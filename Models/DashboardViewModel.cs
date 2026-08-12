namespace Darge.Web.Models;

public class DashboardViewModel
{
    public string Username { get; set; } = "";

    public int Xp { get; set; }

    public int ProjectCount { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public List<string> Achievements { get; set; } = new();

    public List<Project> Projects { get; set; } = new();
}