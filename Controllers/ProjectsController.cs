using System.Security.Claims;
using Darge.Web.Data;
using Darge.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;


[Authorize]
public class ProjectsController : Controller
{
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
        int projectId
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
}