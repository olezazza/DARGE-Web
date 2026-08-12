using System.Security.Claims;
using Darge.Web.Data;
using Darge.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;


[Authorize]
public class DashboardController : Controller
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


    public IActionResult Index()
    {
        return View(
            BuildDashboard()
        );
    }


    public IActionResult Profile()
    {
        return View(
            BuildDashboard()
        );
    }


    private DashboardViewModel BuildDashboard()
    {
        AchievementRepository.CheckAchievements(UserId);

        List<Project> projects =
            ProjectRepository.GetProjects(
                UserId
            );


        int totalTasks = 0;
        int completedTasks = 0;


        foreach (Project project in projects)
        {
            totalTasks +=
                project.Tasks.Count;


            foreach (
                ProjectTask task
                in project.Tasks
            )
            {
                if (task.IsCompleted)
                {
                    completedTasks++;
                }
            }
        }


        int xp = 0;


        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT xp
                FROM users
                WHERE id = $1;
                """
            );


        command.Parameters.AddWithValue(
            UserId
        );


        object? xpResult =
            command.ExecuteScalar();


        if (xpResult != null)
        {
            xp =
                Convert.ToInt32(
                    xpResult
                );
        }


        return new DashboardViewModel
        {
            Username =
                User.Identity?.Name ?? "",

            Xp =
                xp,

            ProjectCount =
                projects.Count,

            TotalTasks =
                totalTasks,

            CompletedTasks =
                completedTasks,

            Projects =
                projects,

            Achievements =
                AchievementRepository
                    .GetAchievements(
                        UserId
                    )
        };
    }
}