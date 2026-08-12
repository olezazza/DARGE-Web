using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Darge.Web.Data;
using Darge.Web.Models;
using Darge.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;


[Authorize]
public class ProjectsController : Controller
{
    private readonly IDargeAiService _aiService;

    public ProjectsController(IDargeAiService aiService)
    {
        _aiService = aiService;
    }

    private int UserId
    {
        get
        {
            string? value =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            return int.Parse(value!);
        }
    }


    [HttpGet]
    public IActionResult Index()
    {
        List<Project> projects =
            ProjectRepository.GetProjects(
                UserId
            );


        return View(projects);
    }


    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(
        string plantName,
        string location,
        string soilType,
        double space
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                plantName
            )
            ||
            space <= 0
        )
        {
            ViewBag.Error =
                "Enter valid project information.";

            return View();
        }


        Project project =
            ProjectRepository.CreateProject(
                UserId,
                plantName,
                location ?? "",
                soilType ?? "",
                space
            );


        AchievementRepository
            .CheckAchievements(
                UserId
            );


        return RedirectToAction(
            "Details",
            new
            {
                id = project.Id
            }
        );
    }


    [HttpGet]
    public IActionResult Plan()
    {
        return View(new AiProjectPlanRequest { Space = 2.0 });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePlan(AiProjectPlanRequest request)
    {
        if (request.Space <= 0)
        {
            ViewBag.Error = "Please specify a valid space (m²).";
            return View("Plan", request);
        }

        try
        {
            AiProjectPlanResponse plan = await _aiService.GenerateProjectPlanAsync(request, UserId);
            ViewBag.OriginalRequest = request;
            return View("ReviewPlan", plan);
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"AI Generation Error: {ex.Message}";
            return View("Plan", request);
        }
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateFromPlan(
        string plantName,
        string location,
        string soilType,
        double space,
        string tasksBase64
    )
    {
        if (string.IsNullOrWhiteSpace(plantName) || space <= 0)
        {
            TempData["Error"] = "Invalid project attributes.";
            return RedirectToAction("Plan");
        }

        var tasks = new List<AiTaskPlan>();
        if (!string.IsNullOrWhiteSpace(tasksBase64))
        {
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(tasksBase64));
                var parsed = JsonSerializer.Deserialize<List<AiTaskPlan>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null)
                {
                    tasks = parsed;
                }
            }
            catch
            {
                // Fall back gracefully
            }
        }

        Project project = ProjectRepository.CreateProjectWithTasks(
            UserId,
            plantName,
            location ?? "",
            soilType ?? "",
            space,
            tasks
        );

        AchievementRepository.CheckAchievements(UserId);

        return RedirectToAction("Details", new { id = project.Id });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AskAi(int projectId, int conversationId, string question)
    {
        Project? project = ProjectRepository.GetProject(projectId, UserId);
        if (project == null) return NotFound();

        AiConversation? conv = AiRepository.GetConversation(conversationId, UserId);
        if (conv == null || conv.ProjectId != projectId)
        {
            TempData["AiError"] = "Invalid project conversation.";
            return RedirectToAction("Assistant", new { id = projectId });
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return RedirectToAction("Assistant", new { id = projectId, conversationId = conversationId });
        }

        string cleanQuestion = question.Trim();
        if (cleanQuestion.Length > 500) cleanQuestion = cleanQuestion.Substring(0, 500);

        if (conv.Messages.Count == 0 || conv.Title.EndsWith("AI Assistant"))
        {
            string newTitle = cleanQuestion.Length > 30 ? cleanQuestion.Substring(0, 30) + "..." : cleanQuestion;
            using var cmd = Database.DataSource.CreateCommand("UPDATE ai_conversations SET title = $1 WHERE id = $2 AND user_id = $3;");
            cmd.Parameters.AddWithValue(newTitle);
            cmd.Parameters.AddWithValue(conversationId);
            cmd.Parameters.AddWithValue(UserId);
            cmd.ExecuteNonQuery();
        }

        AiRepository.AddMessage(conversationId, "user", cleanQuestion);

        try
        {
            var updatedConv = AiRepository.GetConversation(conversationId, UserId)!;
            ProposedActionContainer result = await _aiService.AskProjectAssistantConversationAsync(project, updatedConv.Messages, cleanQuestion, UserId);

            AiRepository.AddMessage(conversationId, "assistant", result.UserTextResponse);

            if (result.Actions != null && result.Actions.Count > 0)
            {
                string json = JsonSerializer.Serialize(result.Actions);
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                TempData["ProposedActionsBase64"] = base64;
                TempData["ProposedActionsProjectId"] = projectId;
            }
        }
        catch (Exception ex)
        {
            TempData["AiError"] = $"AI Assistant Error: {ex.Message}";
        }

        return RedirectToAction("Assistant", new { id = projectId, conversationId = conversationId });
    }

    [HttpGet]
    public IActionResult Assistant(int id, int? conversationId)
    {
        Project? project = ProjectRepository.GetProject(id, UserId);
        if (project == null)
        {
            return NotFound();
        }

        var conversations = AiRepository.GetConversations(UserId, id);
        ViewBag.Conversations = conversations;

        AiConversation? activeConv = null;
        if (conversationId.HasValue)
        {
            activeConv = AiRepository.GetConversation(conversationId.Value, UserId);
        }

        if (activeConv == null && conversations.Count > 0)
        {
            activeConv = AiRepository.GetConversation(conversations[0].Id, UserId);
        }

        if (activeConv == null)
        {
            activeConv = AiRepository.CreateConversation(UserId, id, $"{project.PlantName} AI Assistant");
            conversations = AiRepository.GetConversations(UserId, id);
            ViewBag.Conversations = conversations;
        }

        ViewBag.ActiveConversation = activeConv;
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApplyProposedActions(int projectId, int conversationId, string actionsBase64)
    {
        Project? project = ProjectRepository.GetProject(projectId, UserId);
        if (project == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(actionsBase64))
        {
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(actionsBase64));
                var actions = JsonSerializer.Deserialize<List<ProposedAction>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        if (action.ActionType == "ADD_TASK" && !string.IsNullOrWhiteSpace(action.TaskTitle))
                        {
                            string level = action.Difficulty switch
                            {
                                "Easy" or "easy" => "Easy",
                                "Medium" or "medium" => "Medium",
                                "Hard" or "hard" => "Hard",
                                _ => "Easy"
                            };

                            TaskRepository.CreateTask(
                                projectId,
                                UserId,
                                action.TaskTitle.Length > 100 ? action.TaskTitle.Substring(0, 100) : action.TaskTitle,
                                action.TaskDescription != null && action.TaskDescription.Length > 500 ? action.TaskDescription.Substring(0, 500) : (action.TaskDescription ?? ""),
                                level
                            );
                        }
                        else if (action.ActionType == "UPDATE_PROJECT_INFO")
                        {
                            using var connection = Database.DataSource.OpenConnection();
                            using var cmd = connection.CreateCommand();
                            cmd.CommandText = """
                                UPDATE projects
                                SET location = COALESCE(NULLIF($1, ''), location),
                                    soil_type = COALESCE(NULLIF($2, ''), soil_type),
                                    space = CASE WHEN $3 > 0 THEN $3 ELSE space END
                                WHERE id = $4 AND user_id = $5;
                                """;
                            cmd.Parameters.AddWithValue(action.NewLocation ?? "");
                            cmd.Parameters.AddWithValue(action.NewSoilType ?? "");
                            cmd.Parameters.AddWithValue(action.NewSpace.HasValue ? action.NewSpace.Value : 0);
                            cmd.Parameters.AddWithValue(projectId);
                            cmd.Parameters.AddWithValue(UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AchievementRepository.CheckAchievements(UserId);
                    TempData["Message"] = "Proposed changes successfully applied to project!";
                }
            }
            catch (Exception ex)
            {
                TempData["AiError"] = $"Failed to apply changes: {ex.Message}";
            }
        }

        return RedirectToAction("Assistant", new { id = projectId, conversationId = conversationId });
    }


    [HttpGet]
    public IActionResult Details(
        int id
    )
    {
        Project? project =
            ProjectRepository.GetProject(
                id,
                UserId
            );


        if (project == null)
        {
            return NotFound();
        }


        return View(project);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddTask(
        int projectId,
        string taskName,
        string taskDescription,
        string taskLevel
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                taskName
            )
        )
        {
            return RedirectToAction(
                "Details",
                new
                {
                    id = projectId
                }
            );
        }


        TaskRepository.CreateTask(
            projectId,
            UserId,
            taskName,
            taskDescription ?? "",
            taskLevel
        );


        return RedirectToAction(
            "Details",
            new
            {
                id = projectId
            }
        );
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CompleteTask(
        int taskId,
        int projectId,
        string? returnUrl = null
    )
    {
        bool completed =
            TaskRepository.CompleteTask(
                taskId,
                UserId
            );


        if (completed)
        {
            AchievementRepository
                .CheckAchievements(
                    UserId
                );
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(
            "Details",
            new
            {
                id = projectId
            }
        );
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteTask(
        int taskId,
        int projectId
    )
    {
        TaskRepository.DeleteTask(
            taskId,
            UserId
        );


        return RedirectToAction(
            "Details",
            new
            {
                id = projectId
            }
        );
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(
        int id
    )
    {
        ProjectRepository.DeleteProject(
            id,
            UserId
        );

        return RedirectToAction("Index");
    }
}