using System.Security.Claims;
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
    public async Task<IActionResult> AskAi(int projectId, string question)
    {
        Project? project = ProjectRepository.GetProject(projectId, UserId);
        if (project == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            TempData["AiError"] = "Please enter a question.";
            return RedirectToAction("Details", new { id = projectId });
        }

        try
        {
            string answer = await _aiService.AskProjectAssistantAsync(project, question, UserId);
            TempData["AiQuestion"] = question;
            TempData["AiAnswer"] = answer;
        }
        catch (Exception ex)
        {
            TempData["AiError"] = $"AI Assistant Error: {ex.Message}";
        }

        return RedirectToAction("Details", new { id = projectId });
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


    [HttpGet]
    public IActionResult Assistant(int id)
    {
        Project? project = ProjectRepository.GetProject(id, UserId);
        if (project == null)
        {
            return NotFound();
        }
        return View(project);
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