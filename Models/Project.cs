namespace Darge.Web.Models;

public class Project
{
    public int Id { get; set; }

    public string PlantName { get; set; } = "";

    public string Location { get; set; } = "";

    public string SoilType { get; set; } = "";

    public double Space { get; set; }

    public List<ProjectTask> Tasks { get; set; } = new();
}