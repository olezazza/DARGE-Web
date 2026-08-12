namespace Darge.Web.Models;

public class ProjectTask
{
    public int Id { get; set; }

    public string TaskName { get; set; } = "";

    public string TaskDescription { get; set; } = "";

    public string TaskLevel { get; set; } = "";

    public bool IsCompleted { get; set; }

    public int XpReward { get; set; }
}