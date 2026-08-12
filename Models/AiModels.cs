namespace Darge.Web.Models;

public class AiProjectPlanRequest
{
    public string? PlantName { get; set; }
    public string Location { get; set; } = "";
    public string Environment { get; set; } = "";
    public double Space { get; set; }
    public string? SoilType { get; set; }
    public string ExperienceLevel { get; set; } = "Beginner";
    public string? Goal { get; set; }
}

public class AiTaskPlan
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = "Easy";
    public int XpReward { get; set; } = 10;
    public string Timing { get; set; } = "";
}

public class AiProjectPlanResponse
{
    public string ProjectTitle { get; set; } = "";
    public string PlantName { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Recommendations { get; set; } = "";
    public List<AiTaskPlan> Tasks { get; set; } = new();
}

public class AiAssistantRequest
{
    public int ProjectId { get; set; }
    public string Question { get; set; } = "";
}
