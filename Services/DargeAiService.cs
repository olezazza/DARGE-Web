using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Darge.Web.Models;

namespace Darge.Web.Services;

public interface IDargeAiService
{
    Task<AiProjectPlanResponse> GenerateProjectPlanAsync(AiProjectPlanRequest request, int userId);
    Task<string> AskProjectAssistantAsync(Project project, string question, int userId);
}

public class DargeAiService : IDargeAiService
{
    private readonly HttpClient _httpClient;
    private static readonly ConcurrentDictionary<int, DateTime> UserCooldowns = new();
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(3);

    public DargeAiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private string GetApiKey()
    {
        string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY environment variable is missing. Please configure GEMINI_API_KEY.");
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
                throw new InvalidOperationException("Please wait a few seconds before asking AI again.");
            }
        }
        UserCooldowns[userId] = now;
    }

    public async Task<AiProjectPlanResponse> GenerateProjectPlanAsync(AiProjectPlanRequest request, int userId)
    {
        CheckCooldown(userId);

        string apiKey = GetApiKey();
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={apiKey}";

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

            You MUST respond strictly with a single JSON object matching this schema (do NOT surround with markdown ```json backticks):
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
            Provide between 4 to 8 logical, sequential tasks for starting and maintaining this crop.
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.7,
                maxOutputTokens = 2048
            }
        };

        string jsonPayload = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(endpoint, content);
        if (!response.IsSuccessStatusCode)
        {
            string errBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API request failed ({response.StatusCode}): {errBody}");
        }

        string responseJson = await response.Content.ReadAsStringAsync();
        string rawText = ExtractTextFromGeminiResponse(responseJson);

        AiProjectPlanResponse plan;
        try
        {
            plan = JsonSerializer.Deserialize<AiProjectPlanResponse>(rawText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Failed to parse JSON response from Gemini.");
        }
        catch (Exception ex)
        {
            throw new Exception($"AI output validation error: {ex.Message}. Raw text: {rawText}");
        }

        return SanitizeAndValidatePlan(plan, request);
    }

    public async Task<string> AskProjectAssistantAsync(Project project, string question, int userId)
    {
        CheckCooldown(userId);

        if (string.IsNullOrWhiteSpace(question))
        {
            return "Please ask a specific question about your project.";
        }

        string cleanQuestion = question.Trim();
        if (cleanQuestion.Length > 500)
        {
            cleanQuestion = cleanQuestion.Substring(0, 500);
        }

        string apiKey = GetApiKey();
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={apiKey}";

        int completedCount = project.Tasks.Count(t => t.IsCompleted);
        int totalCount = project.Tasks.Count;

        var taskSummaryList = project.Tasks.Select(t => $"- [{ (t.IsCompleted ? "DONE" : "PENDING") }] {t.TaskName} ({t.TaskLevel})").ToList();
        string tasksText = taskSummaryList.Count > 0 ? string.Join("\n", taskSummaryList) : "No tasks created yet.";

        string systemPrompt = $"""
            You are DARGE AI, an expert, friendly gardening assistant embedded inside the DARGE project management app.
            
            Current Project Context:
            - Plant: {project.PlantName}
            - Location: {(string.IsNullOrWhiteSpace(project.Location) ? "Not specified" : project.Location)}
            - Soil: {(string.IsNullOrWhiteSpace(project.SoilType) ? "Not specified" : project.SoilType)}
            - Space: {project.Space} m²
            - Progress: {completedCount}/{totalCount} tasks completed
            - Tasks list:
            {tasksText}

            User Question:
            "{cleanQuestion}"

            Instructions:
            - Provide a concise, practical, and beginner-friendly answer (150-250 words max).
            - Focus directly on the context of this specific plant and project.
            - If the user asks about chemical pesticides or hazardous substances, give cautious safety advice and explicitly recommend following local product label instructions and local agricultural experts.
            - Do not claim uncertain advice is guaranteed.
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = systemPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 1024
            }
        };

        string jsonPayload = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(endpoint, content);
        if (!response.IsSuccessStatusCode)
        {
            string errBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API request failed ({response.StatusCode}): {errBody}");
        }

        string responseJson = await response.Content.ReadAsStringAsync();
        return ExtractTextFromGeminiResponse(responseJson);
    }

    private string ExtractTextFromGeminiResponse(string jsonResponse)
    {
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var text = parts[0].GetProperty("text").GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        throw new Exception("Unexpected response structure from Gemini API.");
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
