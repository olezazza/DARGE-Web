using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
    public IActionResult Index(int? conversationId, int? projectId)
    {
        var userProjects = ProjectRepository.GetProjects(UserId);
        ViewBag.UserProjects = userProjects;

        var allConversations = AiRepository.GetUserConversations(UserId);
        ViewBag.Conversations = allConversations;

        AiConversation? activeConv = null;

        if (conversationId.HasValue)
        {
            activeConv = AiRepository.GetConversation(conversationId.Value, UserId);
        }

        if (activeConv == null && projectId.HasValue && projectId.Value > 0)
        {
            var proj = ProjectRepository.GetProject(projectId.Value, UserId);
            if (proj != null)
            {
                var projConvs = AiRepository.GetConversations(UserId, proj.Id);
                if (projConvs.Count > 0)
                {
                    activeConv = AiRepository.GetConversation(projConvs[0].Id, UserId);
                }
                else
                {
                    activeConv = AiRepository.CreateConversation(UserId, proj.Id, $"{proj.PlantName} Chat");
                    allConversations = AiRepository.GetUserConversations(UserId);
                    ViewBag.Conversations = allConversations;
                }
            }
        }

        if (activeConv == null && allConversations.Count > 0)
        {
            activeConv = AiRepository.GetConversation(allConversations[0].Id, UserId);
        }

        if (activeConv == null)
        {
            activeConv = AiRepository.CreateConversation(UserId, null, "General Gardening Chat");
            allConversations = AiRepository.GetUserConversations(UserId);
            ViewBag.Conversations = allConversations;
        }

        ViewBag.ActiveConversation = activeConv;
        ViewBag.SelectedProjectId = activeConv?.ProjectId;

        if (activeConv?.ProjectId != null)
        {
            ViewBag.ActiveProject = ProjectRepository.GetProject(activeConv.ProjectId.Value, UserId);
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NewChat(int? projectId)
    {
        int? validProjectId = null;
        string defaultTitle = "General Gardening Chat";

        if (projectId.HasValue && projectId.Value > 0)
        {
            var proj = ProjectRepository.GetProject(projectId.Value, UserId);
            if (proj != null)
            {
                validProjectId = proj.Id;
                defaultTitle = $"{proj.PlantName} Chat";
            }
        }

        var conv = AiRepository.CreateConversation(UserId, validProjectId, defaultTitle);
        return RedirectToAction("Index", new { conversationId = conv.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(int conversationId, string question)
    {
        var conv = AiRepository.GetConversation(conversationId, UserId);
        if (conv == null)
        {
            TempData["Error"] = "Invalid conversation session.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return RedirectToAction("Index", new { conversationId = conversationId });
        }

        string cleanQuestion = question.Trim();
        if (cleanQuestion.Length > 500) cleanQuestion = cleanQuestion.Substring(0, 500);

        if (conv.Messages.Count == 0 || conv.Title.EndsWith("Chat"))
        {
            string newTitle = cleanQuestion.Length > 35 ? cleanQuestion.Substring(0, 35) + "..." : cleanQuestion;
            using var cmd = Database.DataSource.CreateCommand("UPDATE ai_conversations SET title = $1 WHERE id = $2 AND user_id = $3;");
            cmd.Parameters.AddWithValue(newTitle);
            cmd.Parameters.AddWithValue(conversationId);
            cmd.Parameters.AddWithValue(UserId);
            cmd.ExecuteNonQuery();
        }

        AiRepository.AddMessage(conversationId, "user", cleanQuestion);

        try
        {
            var updatedConv = AiRepository.GetConversation(conversationId, UserId)!;

            if (conv.ProjectId.HasValue)
            {
                var proj = ProjectRepository.GetProject(conv.ProjectId.Value, UserId);
                if (proj != null)
                {
                    var result = await _aiService.AskProjectAssistantConversationAsync(proj, updatedConv.Messages, cleanQuestion, UserId);
                    AiRepository.AddMessage(conversationId, "assistant", result.UserTextResponse);

                    if (result.Actions != null && result.Actions.Count > 0)
                    {
                        string json = JsonSerializer.Serialize(result.Actions);
                        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                        TempData["ProposedActionsBase64"] = base64;
                        TempData["ProposedActionsProjectId"] = proj.Id;
                    }
                }
                else
                {
                    string responseText = await _aiService.AskGlobalAssistantConversationAsync(updatedConv.Messages, cleanQuestion, UserId);
                    AiRepository.AddMessage(conversationId, "assistant", responseText);
                }
            }
            else
            {
                string responseText = await _aiService.AskGlobalAssistantConversationAsync(updatedConv.Messages, cleanQuestion, UserId);
                AiRepository.AddMessage(conversationId, "assistant", responseText);
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Index", new { conversationId = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteChat(int conversationId)
    {
        AiRepository.DeleteConversation(conversationId, UserId);
        return RedirectToAction("Index");
    }
}
