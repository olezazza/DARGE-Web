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

public class AiConversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<AiMessage> Messages { get; set; } = new();
}

public class AiMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = "user"; // 'user' or 'assistant'
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProposedActionContainer
{
    public string UserTextResponse { get; set; } = "";
    public List<ProposedAction> Actions { get; set; } = new();
}

public class ProposedAction
{
    public string ActionType { get; set; } = ""; // "ADD_TASK", "UPDATE_PROJECT_INFO"
    public string? TaskTitle { get; set; }
    public string? TaskDescription { get; set; }
    public string Difficulty { get; set; } = "Easy";
    public int XpReward { get; set; } = 10;

    public string? NewLocation { get; set; }
    public string? NewSoilType { get; set; }
    public double? NewSpace { get; set; }
}
