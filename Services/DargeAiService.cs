using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Darge.Web.Models;

namespace Darge.Web.Services;

public interface IDargeAiService
{
    Task<AiProjectPlanResponse> GenerateProjectPlanAsync(AiProjectPlanRequest request, int userId);
    Task<ProposedActionContainer> AskProjectAssistantConversationAsync(Project project, List<AiMessage> history, string newQuestion, int userId);
    Task<string> AskGlobalAssistantConversationAsync(List<AiMessage> history, string newQuestion, int userId);
}

public class DargeAiService : IDargeAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _loc;
    private static readonly ConcurrentDictionary<int, DateTime> UserCooldowns = new();
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(2);

    public DargeAiService(HttpClient httpClient, ILocalizationService loc)
    {
        _httpClient = httpClient;
        _loc = loc;
    }

    private bool IsMockMode()
    {
        string? mode = Environment.GetEnvironmentVariable("DARGE_AI_MODE");
        return string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase);
    }

    private string GetApiKey()
    {
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY environment variable is missing. Please configure GEMINI_API_KEY or set DARGE_AI_MODE=Mock for development.");
        }
        return apiKey;
    }

    private void CheckCooldown(int userId)
    {
        DateTime now = DateTime.UtcNow;
        if (UserCooldowns.TryGetValue(userId, out DateTime lastTime))
        {
            if (now - lastTime < CooldownDuration)
            {
                throw new InvalidOperationException(_loc.Get("AiQuotaExceeded"));
            }
        }
        UserCooldowns[userId] = now;
    }

    private string GetModel()
    {
        string? envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        if (!string.IsNullOrWhiteSpace(envModel))
        {
            return envModel.Trim();
        }
        return "gemini-3.5-flash";
    }

    private static string CleanJsonContent(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return "";

        string text = rawText.Trim();

        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(7);
        }
        else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(3);
        }

        if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 3);
        }

        text = text.Trim();

        int startIdx = text.IndexOf('{');
        int endIdx = text.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            text = text.Substring(startIdx, endIdx - startIdx + 1);
        }

        return text;
    }

    private async Task<string> ExecuteGeminiRequestWithRetryAsync(
        Func<string, object> payloadFactory,
        bool isStructuredOutput,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        string apiKey = GetApiKey();
        string model = GetModel();
        string safeEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        string requestUri = $"{safeEndpoint}?key={apiKey}";
        object payload = payloadFactory(model);
        string jsonPayload = JsonSerializer.Serialize(payload);

        int maxAttempts = 3;
        var random = new Random();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            HttpResponseMessage? response = null;

            try
            {
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
                stopwatch.Stop();

                int statusCode = (int)response.StatusCode;

                // SUCCESS
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[DARGE AI Diagnostic] Success | Purpose={purpose} | Model={model} | Attempt={attempt}/{maxAttempts} | Duration={stopwatch.ElapsedMilliseconds}ms | Structured={isStructuredOutput}");
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }

                // RATE LIMIT / QUOTA (429) -> DO NOT RETRY
                if (statusCode == 429)
                {
                    Console.WriteLine($"[DARGE AI Quota Diagnostic] Status=429 (Resource Exhausted) | Purpose={purpose} | Model={model} | Duration={stopwatch.ElapsedMilliseconds}ms");
                    throw new InvalidOperationException(_loc.Get("AiQuotaExceeded"));
                }

                string errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                string safeErrSnippet = errBody.Length > 300 ? errBody.Substring(0, 300) + "..." : errBody;

                if (errBody.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                    errBody.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[DARGE AI Quota Diagnostic] Quota exhausted in response body | Purpose={purpose} | Model={model}");
                    throw new InvalidOperationException(_loc.Get("AiQuotaExceeded"));
                }

                // CLIENT ERRORS (400, 401, 403, 404) -> DO NOT RETRY (Non-transient)
                if (statusCode >= 400 && statusCode < 500)
                {
                    Console.WriteLine($"[DARGE AI Diagnostic Error] Client Error {statusCode} ({response.ReasonPhrase}) | Purpose={purpose} | Model={model} | Endpoint={safeEndpoint} | Attempt={attempt}/{maxAttempts} | Duration={stopwatch.ElapsedMilliseconds}ms | Structured={isStructuredOutput} | Details={safeErrSnippet}");
                    throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
                }

                // TRANSIENT SERVER ERRORS (500, 502, 503, 504) -> RETRY WITH BACKOFF & JITTER
                Console.WriteLine($"[DARGE AI Diagnostic Warning] Transient Error {statusCode} ({response.ReasonPhrase}) | Purpose={purpose} | Model={model} | Endpoint={safeEndpoint} | Attempt={attempt}/{maxAttempts} | Duration={stopwatch.ElapsedMilliseconds}ms | Structured={isStructuredOutput} | Details={safeErrSnippet}");

                if (attempt < maxAttempts)
                {
                    int baseDelayMs = attempt == 1 ? 1000 : 2000;
                    int jitterMs = random.Next(-150, 250);
                    int totalDelayMs = Math.Max(500, baseDelayMs + jitterMs);

                    Console.WriteLine($"[DARGE AI Retry] Waiting {totalDelayMs}ms before attempt {attempt + 1}/{maxAttempts} for {purpose} with model {model}...");
                    await Task.Delay(totalDelayMs, cancellationToken);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts && (ex is HttpRequestException || ex is TaskCanceledException))
            {
                stopwatch.Stop();
                Console.WriteLine($"[DARGE AI Diagnostic Warning] Network/Timeout ({ex.GetType().Name}: {ex.Message}) | Purpose={purpose} | Model={model} | Endpoint={safeEndpoint} | Attempt={attempt}/{maxAttempts} | Duration={stopwatch.ElapsedMilliseconds}ms");

                int baseDelayMs = attempt == 1 ? 1000 : 2000;
                int jitterMs = random.Next(-150, 250);
                int totalDelayMs = Math.Max(500, baseDelayMs + jitterMs);

                Console.WriteLine($"[DARGE AI Retry] Waiting {totalDelayMs}ms before attempt {attempt + 1}/{maxAttempts}...");
                await Task.Delay(totalDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"[DARGE AI Diagnostic Error] Unhandled Exception ({ex.GetType().Name}: {ex.Message}) | Purpose={purpose} | Model={model} | Endpoint={safeEndpoint} | Attempt={attempt}/{maxAttempts} | Duration={stopwatch.ElapsedMilliseconds}ms");
                throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
            }
            finally
            {
                response?.Dispose();
            }
        }

        Console.WriteLine($"[DARGE AI Diagnostic Error] All {maxAttempts} attempts failed for {purpose} with model {model}.");
        throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
    }

    public async Task<AiProjectPlanResponse> GenerateProjectPlanAsync(AiProjectPlanRequest request, int userId)
    {
        CheckCooldown(userId);

        if (IsMockMode())
        {
            Console.WriteLine("[DARGE AI Diagnostic] Using Mock AI Provider (DARGE_AI_MODE=Mock) for Project Plan Generation.");
            return GetMockProjectPlan(request);
        }

        string prompt = $$"""
            You are DARGE AI, an expert gardening assistant.
            Generate a detailed, beginner-friendly growing project plan based on these details:
            - Target Plant: {{(string.IsNullOrWhiteSpace(request.PlantName) ? "Suggest the best plant based on location/environment" : request.PlantName)}}
            - Location/Region: {{request.Location}}
            - Growing Environment: {{request.Environment}}
            - Space: {{request.Space}} m²
            - Soil Type: {{(string.IsNullOrWhiteSpace(request.SoilType) ? "Not specified" : request.SoilType)}}
            - Experience Level: {{request.ExperienceLevel}}
            - User Goal/Notes: {{(string.IsNullOrWhiteSpace(request.Goal) ? "Edible or ornamental crop" : request.Goal.Substring(0, Math.Min(request.Goal.Length, 500)))}}

            LANGUAGE & LOCALIZATION INSTRUCTIONS:
            - Automatically detect the primary language of the input text (PlantName, Location, SoilType, Goal).
            - If the user provided inputs in Georgian (ქართული) or requested Georgian output, you MUST generate all human-facing JSON string values in fluent, natural Georgian.
            - If the user provided inputs in English, generate all string values in English.
            - Keep difficulty strictly as "Easy", "Medium", or "Hard".

            You MUST respond strictly with a single JSON object matching this schema:
            {
              "projectTitle": "String - Short title of project",
              "plantName": "String - Specific plant name",
              "summary": "String - 2-3 sentence overview of this growing project",
              "recommendations": "String - Key care tips, sunlight, and watering guidance",
              "tasks": [
                {
                  "title": "String - Actionable task name",
                  "description": "String - Short step by step detail",
                  "difficulty": "Easy, Medium, or Hard",
                  "xpReward": 10, 20, or 30 (10 for Easy, 20 for Medium, 30 for Hard),
                  "timing": "String - e.g. Week 1, Day 3"
                }
              ]
            }
            Provide between 4 to 8 logical tasks.
            """;

        string responseJson = await ExecuteGeminiRequestWithRetryAsync(
            payloadFactory: model => new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.7,
                    maxOutputTokens = 8192
                }
            },
            isStructuredOutput: true,
            purpose: "GenerateProjectPlan"
        );

        string rawText = ExtractTextFromGeminiResponse(responseJson);
        string cleanJson = CleanJsonContent(rawText);

        AiProjectPlanResponse plan;
        try
        {
            plan = JsonSerializer.Deserialize<AiProjectPlanResponse>(cleanJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Deserialized plan was null.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DARGE AI Plan Parse Error] Failed to parse JSON: {ex.Message}. Raw text: {rawText}");
            throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
        }

        return SanitizeAndValidatePlan(plan, request);
    }

    public async Task<string> AskGlobalAssistantConversationAsync(List<AiMessage> history, string newQuestion, int userId)
    {
        CheckCooldown(userId);

        string cleanQuestion = newQuestion.Trim();
        if (cleanQuestion.Length > 500) cleanQuestion = cleanQuestion.Substring(0, 500);

        if (IsMockMode())
        {
            Console.WriteLine("[DARGE AI Diagnostic] Using Mock AI Provider (DARGE_AI_MODE=Mock) for Global Assistant.");
            return GetMockGlobalResponse(cleanQuestion);
        }

        var contentsList = new List<object>();

        string systemContext = """
            You are DARGE AI, an expert, friendly agricultural and gardening assistant.
            Instructions:
            - Automatically detect the primary language of the User's latest question (e.g. Georgian ქართული vs English).
            - You MUST respond in the EXACT SAME language as the user's latest question. If Georgian, reply entirely in natural, fluent Georgian (ქართულად).
            - Maintain conversational continuity using the provided message history.
            - Give practical, concise, and clear advice.
            - If asked about chemical pesticides, provide cautious safety guidance and recommend following product label instructions and local agricultural experts.
            """;

        contentsList.Add(new { role = "user", parts = new[] { new { text = systemContext } } });
        contentsList.Add(new { role = "model", parts = new[] { new { text = "Understood. I am DARGE AI, ready to assist in English or Georgian." } } });

        foreach (var msg in history.TakeLast(10))
        {
            string role = msg.Role == "assistant" ? "model" : "user";
            contentsList.Add(new { role = role, parts = new[] { new { text = msg.Content } } });
        }

        contentsList.Add(new { role = "user", parts = new[] { new { text = cleanQuestion } } });

        string responseJson = await ExecuteGeminiRequestWithRetryAsync(
            payloadFactory: model => new
            {
                contents = contentsList,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 4096
                }
            },
            isStructuredOutput: false,
            purpose: "GlobalAssistant"
        );

        return ExtractTextFromGeminiResponse(responseJson);
    }

    public async Task<ProposedActionContainer> AskProjectAssistantConversationAsync(Project project, List<AiMessage> history, string newQuestion, int userId)
    {
        CheckCooldown(userId);

        string cleanQuestion = newQuestion.Trim();
        if (cleanQuestion.Length > 500) cleanQuestion = cleanQuestion.Substring(0, 500);

        if (IsMockMode())
        {
            Console.WriteLine("[DARGE AI Diagnostic] Using Mock AI Provider (DARGE_AI_MODE=Mock) for Project Assistant.");
            return GetMockProjectResponse(project, cleanQuestion);
        }

        int completedCount = project.Tasks.Count(t => t.IsCompleted);
        int totalCount = project.Tasks.Count;
        var taskSummaryList = project.Tasks.Select(t => $"- [{ (t.IsCompleted ? "DONE" : "PENDING") }] {t.TaskName} ({t.TaskLevel})").ToList();
        string tasksText = taskSummaryList.Count > 0 ? string.Join("\n", taskSummaryList) : "No tasks created yet.";

        string systemPrompt = $$"""
            You are DARGE AI, an expert, friendly gardening assistant embedded inside the DARGE project management app.
            
            Current Project Context:
            - Plant: {{project.PlantName}}
            - Location: {{(string.IsNullOrWhiteSpace(project.Location) ? "Not specified" : project.Location)}}
            - Soil: {{(string.IsNullOrWhiteSpace(project.SoilType) ? "Not specified" : project.SoilType)}}
            - Space: {{project.Space}} m²
            - Progress: {{completedCount}}/{{totalCount}} tasks completed
            - Tasks list:
            {{tasksText}}

            Instructions:
            - Automatically detect the primary language of the User Question (e.g. Georgian ქართული vs English).
            - You MUST respond in the EXACT SAME language as the User Question. If Georgian, reply entirely in natural, fluent Georgian.
            - If the user asks to add tasks or update project details (location, soil, space), respond conversationally AND ALSO include structured action proposals.
            - If proposing actions, append a JSON block at the very end of your response formatted EXACTLY like this:
            ```action
            [
              {
                "actionType": "ADD_TASK",
                "taskTitle": "Task Name Here",
                "taskDescription": "Short description",
                "difficulty": "Easy, Medium, or Hard",
                "xpReward": 10
              },
              {
                "actionType": "UPDATE_PROJECT_INFO",
                "newLocation": "New Location",
                "newSoilType": "New Soil",
                "newSpace": 5.0
              }
            ]
            ```
            - If no changes are needed or requested, do NOT include the ```action block.
            """;

        var contentsList = new List<object>();
        contentsList.Add(new { role = "user", parts = new[] { new { text = systemPrompt } } });
        contentsList.Add(new { role = "model", parts = new[] { new { text = $"Understood. I am assistant for {project.PlantName}." } } });

        foreach (var msg in history.TakeLast(10))
        {
            string role = msg.Role == "assistant" ? "model" : "user";
            contentsList.Add(new { role = role, parts = new[] { new { text = msg.Content } } });
        }

        contentsList.Add(new { role = "user", parts = new[] { new { text = cleanQuestion } } });

        string responseJson = await ExecuteGeminiRequestWithRetryAsync(
            payloadFactory: model => new
            {
                contents = contentsList,
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 4096
                }
            },
            isStructuredOutput: false,
            purpose: "ProjectAssistant"
        );

        string rawText = ExtractTextFromGeminiResponse(responseJson);
        return ParseResponseAndActions(rawText);
    }

    private ProposedActionContainer ParseResponseAndActions(string rawText)
    {
        var result = new ProposedActionContainer();

        int actionIdx = rawText.IndexOf("```action", StringComparison.OrdinalIgnoreCase);
        if (actionIdx >= 0)
        {
            string conversationalText = rawText.Substring(0, actionIdx).Trim();
            result.UserTextResponse = conversationalText;

            int endIdx = rawText.IndexOf("```", actionIdx + 9);
            if (endIdx > actionIdx)
            {
                string jsonBlock = rawText.Substring(actionIdx + 9, endIdx - (actionIdx + 9)).Trim();
                try
                {
                    var parsedActions = JsonSerializer.Deserialize<List<ProposedAction>>(jsonBlock, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (parsedActions != null)
                    {
                        result.Actions = parsedActions;
                    }
                }
                catch {}
            }
        }
        else
        {
            result.UserTextResponse = rawText.Trim();
        }

        return result;
    }

    private string ExtractTextFromGeminiResponse(string jsonResponse)
    {
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];

            string finishReason = "UNKNOWN";
            if (firstCandidate.TryGetProperty("finishReason", out var reasonElem))
            {
                finishReason = reasonElem.GetString() ?? "UNKNOWN";
            }

            if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    // In Gemini models with reasoning/thinking, skip internal thought parts
                    if (part.TryGetProperty("thought", out var isThought) && isThought.GetBoolean())
                    {
                        continue;
                    }

                    if (part.TryGetProperty("text", out var textElem))
                    {
                        string? partText = textElem.GetString();
                        if (!string.IsNullOrEmpty(partText))
                        {
                            sb.Append(partText);
                        }
                    }
                }

                string combinedText = sb.ToString().Trim();
                Console.WriteLine($"[DARGE AI Diagnostic] Candidates={candidates.GetArrayLength()}, FinishReason={finishReason}, ExtractedTextLength={combinedText.Length}");

                if (!string.IsNullOrWhiteSpace(combinedText))
                {
                    return combinedText;
                }

                // Fallback to first part text if thought filtering excluded everything
                if (parts[0].TryGetProperty("text", out var firstTextElem))
                {
                    var text = firstTextElem.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
        }

        if (root.TryGetProperty("promptFeedback", out var promptFeedback))
        {
            if (promptFeedback.TryGetProperty("blockReason", out var blockReason))
            {
                Console.WriteLine($"[DARGE AI Diagnostic Warning] Prompt blocked: {blockReason.GetString()}");
                throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
            }
        }

        Console.WriteLine($"[DARGE AI Diagnostic Error] Unexpected response structure: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 300))}");
        throw new InvalidOperationException(_loc.Get("AiTemporarilyUnavailable"));
    }

    private AiProjectPlanResponse GetMockProjectPlan(AiProjectPlanRequest request)
    {
        bool isGeorgian = ContainsGeorgian(request.PlantName ?? "") || ContainsGeorgian(request.Location ?? "");
        string plant = !string.IsNullOrWhiteSpace(request.PlantName) ? request.PlantName : (isGeorgian ? "ორგანული პომიდორი" : "Organic Heirloom Tomatoes");

        if (isGeorgian)
        {
            return new AiProjectPlanResponse
            {
                ProjectTitle = $"{plant} - AI გეგმა",
                PlantName = plant,
                Summary = $"მზის მოყვარული {plant}-ის მოშენების გეგმა {request.Space} მ² ფართობზე. მოიცავს ნიადაგის მომზადებას, რეგულარულ მორწყვასა და მავნებლებისგან ბუნებრივ დაცვას.",
                Recommendations = "უზრუნველყავით მინიმუმ 6 საათი მზის სინათლე. მორწყეთ დილით ადრე ნიადაგის ძირში.",
                Tasks = new List<AiTaskPlan>
                {
                    new() { Title = "ქოთნის/საწოლის ნიადაგის მომზადება", Description = "აურიეთ ორგანული კომპოსტი და პერლიტი ნიადაგის აერაციისთვის.", Difficulty = "Easy", XpReward = 10, Timing = "დღე 1" },
                    new() { Title = "ჩითილების დარგვა და წყლით უზრუნველყოფა", Description = "დაარგეთ ჩითილები 45 სმ დაშორებით და უხვად მორწყეთ.", Difficulty = "Easy", XpReward = 10, Timing = "დღე 2" },
                    new() { Title = "საყრდენი ჭიგოების დამონტაჟება", Description = "ჩაასობეთ საყრდენი ჯოხები მცენარის გვერდით ნაყოფის სიმძიმის დასაჭერად.", Difficulty = "Medium", XpReward = 20, Timing = "კვირა 2" },
                    new() { Title = "ორგანული სასუქის მიწოდება", Description = "დაამატეთ თხევადი ორგანული სასუქი აზოტისა და კალიუმის ბალანსისთვის.", Difficulty = "Medium", XpReward = 20, Timing = "კვირა 4" },
                    new() { Title = "მოსავლის აღება", Description = "მოკრიფეთ მწიფე, წითელი ნაყოფი ფრთხილად.", Difficulty = "Hard", XpReward = 30, Timing = "კვირა 8" }
                }
            };
        }

        return new AiProjectPlanResponse
        {
            ProjectTitle = $"{plant} Growth Plan",
            PlantName = plant,
            Summary = $"Tailored growing guide for {plant} across {request.Space} m². Designed for optimal yields using natural compost and automated care steps.",
            Recommendations = "Provide at least 6 hours of direct sunlight. Water early morning at the base of the stems.",
            Tasks = new List<AiTaskPlan>
            {
                new() { Title = "Prepare Potting Soil & Soil Bed", Description = "Mix organic compost with drainage perlite for optimal root growth.", Difficulty = "Easy", XpReward = 10, Timing = "Day 1" },
                new() { Title = "Plant Seedlings & Initial Deep Watering", Description = "Space seedlings 45cm apart and soak root zones thoroughly.", Difficulty = "Easy", XpReward = 10, Timing = "Day 2" },
                new() { Title = "Install Support Stakes & Cages", Description = "Secure vertical wooden stakes to support heavy fruiting branches.", Difficulty = "Medium", XpReward = 20, Timing = "Week 2" },
                new() { Title = "Apply Organic Potassium Fertilizer", Description = "Feed with balanced liquid kelp fertilizer to encourage blooming.", Difficulty = "Medium", XpReward = 20, Timing = "Week 4" },
                new() { Title = "Harvest Peak Fruit", Description = "Gently twist ripe fruit off vines when fully colored.", Difficulty = "Hard", XpReward = 30, Timing = "Week 8" }
            }
        };
    }

    private string GetMockGlobalResponse(string question)
    {
        bool isGeorgian = ContainsGeorgian(question);

        if (isGeorgian)
        {
            return $"🧪 [AI Mock Mode Active]\n\n" +
                   $"გმადლობთ კითხვისთვის: \"{question}\"\n\n" +
                   $"მცენარის ჯანსაღი ზრდისთვის მნიშვნელოვანია:\n" +
                   $"1. ☀️ **მზის განათება**: უზრუნველყავით მინიმუმ 5-7 საათი პირდაპირი სინათლე.\n" +
                   $"2. 💧 **მორწყვის რეჟიმი**: შეამოწმეთ ნიადაგის ტენიანობა თითის 2-3 სმ-ით ჩაყოფით.\n" +
                   $"3. 🪨 **დრენაჟი**: გამოიყენეთ ხვრელებიანი ქოთნები ფესვების ლპობის თავიდან ასაცილებლად.";
        }

        return $"🧪 [AI Mock Mode Active]\n\n" +
               $"Thank you for asking: \"{question}\"\n\n" +
               $"Here are essential guidelines for optimal plant growth:\n" +
               $"1. ☀️ **Sunlight Exposure**: Ensure 5–7 hours of direct sunlight daily.\n" +
               $"2. 💧 **Watering Discipline**: Check topsoil moisture before watering; avoid soggy roots.\n" +
               $"3. 🪨 **Soil Drainage**: Ensure pots have adequate drainage holes and aeration perlite.";
    }

    private ProposedActionContainer GetMockProjectResponse(Project project, string question)
    {
        bool isGeorgian = ContainsGeorgian(question);
        var container = new ProposedActionContainer();

        if (isGeorgian)
        {
            container.UserTextResponse = $"🧪 [AI Mock Mode Active]\n\n" +
                $"პროექტის \"{project.PlantName}\" მიმდინარე სტატუსი გაანალიზებულია.\n" +
                $"თქვენი კითხვის საფუძველზე შემოთავაზებულია ახალი დავალების დამატება პროექტში.";
        }
        else
        {
            container.UserTextResponse = $"🧪 [AI Mock Mode Active]\n\n" +
                $"Analyzed current status for project \"{project.PlantName}\".\n" +
                $"Based on your request, I propose adding a recommended task to your project workflow.";
        }

        container.Actions = new List<ProposedAction>
        {
            new ProposedAction
            {
                ActionType = "ADD_TASK",
                TaskTitle = isGeorgian ? "ჩრდილის ბადის დამონტაჟება" : "Install Afternoon Shade Net",
                TaskDescription = isGeorgian ? "დაიცავით ფოთლები შუადღის მზის დამწვრობისგან." : "Protect foliage during peak afternoon sunlight.",
                Difficulty = "Easy",
                XpReward = 10
            }
        };

        return container;
    }

    private bool ContainsGeorgian(string text)
    {
        return text.Any(c => c >= 0x10A0 && c <= 0x10FF);
    }

    private AiProjectPlanResponse SanitizeAndValidatePlan(AiProjectPlanResponse plan, AiProjectPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(plan.PlantName))
        {
            plan.PlantName = !string.IsNullOrWhiteSpace(request.PlantName) ? request.PlantName : "Garden Plant";
        }
        if (string.IsNullOrWhiteSpace(plan.ProjectTitle))
        {
            plan.ProjectTitle = $"{plan.PlantName} Project";
        }

        if (plan.PlantName.Length > 100) plan.PlantName = plan.PlantName.Substring(0, 100);
        if (plan.ProjectTitle.Length > 100) plan.ProjectTitle = plan.ProjectTitle.Substring(0, 100);
        if (plan.Summary.Length > 1000) plan.Summary = plan.Summary.Substring(0, 1000);
        if (plan.Recommendations.Length > 1000) plan.Recommendations = plan.Recommendations.Substring(0, 1000);

        var validTasks = new List<AiTaskPlan>();
        if (plan.Tasks != null)
        {
            foreach (var task in plan.Tasks.Take(10))
            {
                if (string.IsNullOrWhiteSpace(task.Title)) continue;

                string level = task.Difficulty switch
                {
                    "Easy" or "easy" => "Easy",
                    "Medium" or "medium" => "Medium",
                    "Hard" or "hard" => "Hard",
                    _ => "Easy"
                };

                int xp = level switch
                {
                    "Easy" => 10,
                    "Medium" => 20,
                    "Hard" => 30,
                    _ => 10
                };

                validTasks.Add(new AiTaskPlan
                {
                    Title = task.Title.Length > 100 ? task.Title.Substring(0, 100) : task.Title,
                    Description = task.Description != null && task.Description.Length > 500 ? task.Description.Substring(0, 500) : (task.Description ?? ""),
                    Difficulty = level,
                    XpReward = xp,
                    Timing = task.Timing != null && task.Timing.Length > 50 ? task.Timing.Substring(0, 50) : (task.Timing ?? "")
                });
            }
        }

        plan.Tasks = validTasks;
        if (plan.Tasks.Count == 0)
        {
            throw new Exception("AI generated 0 valid tasks for this project. Please try again.");
        }

        return plan;
    }
}
