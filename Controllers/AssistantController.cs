using System.Security.Claims;
using Darge.Web.Data;
using Darge.Web.Models;
using Darge.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Darge.Web.Controllers;

[Authorize]
public class AssistantController : Controller
{
    private readonly IDargeAiService _aiService;

    public AssistantController(IDargeAiService aiService)
    {
        _aiService = aiService;
    }

    private int UserId
    {
        get
        {
            string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.Parse(value!);
        }
    }

    [HttpGet]
    public IActionResult Index(int? projectId)
    {
        var projects = ProjectRepository.GetProjects(UserId);
        ViewBag.Projects = projects;
        ViewBag.SelectedProjectId = projectId;

        if (projectId.HasValue)
        {
            var selectedProject = projects.FirstOrDefault(p => p.Id == projectId.Value);
            ViewBag.SelectedProject = selectedProject;
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(int? projectId, string question)
    {
        var projects = ProjectRepository.GetProjects(UserId);
        ViewBag.Projects = projects;
        ViewBag.SelectedProjectId = projectId;

        if (string.IsNullOrWhiteSpace(question))
        {
            ViewBag.Error = "Please enter a question for DARGE AI.";
            return View("Index");
        }

        Project? projectContext = null;
        if (projectId.HasValue && projectId.Value > 0)
        {
            projectContext = projects.FirstOrDefault(p => p.Id == projectId.Value);
            if (projectContext == null)
            {
                ViewBag.Error = "Invalid project selection.";
                return View("Index");
            }
        }

        ViewBag.SelectedProject = projectContext;
        ViewBag.LastQuestion = question;

        try
        {
            if (projectContext != null)
            {
                string answer = await _aiService.AskProjectAssistantAsync(projectContext, question, UserId);
                ViewBag.Answer = answer;
            }
            else
            {
                string generalPrompt = $"""
                    You are DARGE AI, an expert, friendly gardening and agricultural assistant.
                    Answer this general gardening question concisely, practical, and beginner-friendly (150-250 words max):
                    "{question.Trim()}"
                    If asked about chemical pesticides, provide safety warnings and recommend following local product label instructions.
                    """;

                // Use project assistant service or general prompt
                var dummyProj = new Project
                {
                    PlantName = "General Gardening Query",
                    Location = "N/A",
                    SoilType = "N/A",
                    Space = 0
                };
                string answer = await _aiService.AskProjectAssistantAsync(dummyProj, question, UserId);
                ViewBag.Answer = answer;
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"AI Error: {ex.Message}";
        }

        return View("Index");
    }
}
