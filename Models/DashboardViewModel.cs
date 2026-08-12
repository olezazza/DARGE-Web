namespace Darge.Web.Models;

public class DashboardViewModel
{
    public string Username { get; set; } = "";

    public int Xp { get; set; }

    public int Level => (Xp / 50) + 1;

    public int XpInCurrentLevel => Xp % 50;

    public int XpToNextLevel => 50;

    public double LevelProgress => (double)XpInCurrentLevel / XpToNextLevel * 100;

    public int ProjectCount { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public int IncompleteTasks => Math.Max(0, TotalTasks - CompletedTasks);

    public double TaskProgressPercent => TotalTasks == 0 ? 0 : Math.Round((double)CompletedTasks / TotalTasks * 100);

    public List<string> Achievements { get; set; } = new();

    public List<Project> Projects { get; set; } = new();
}