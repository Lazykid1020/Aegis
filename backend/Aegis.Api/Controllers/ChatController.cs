using Microsoft.AspNetCore.Mvc;
using Aegis.Api.Models;
using Aegis.Api.Services;

namespace Aegis.Api.Controllers;

/// <summary>
/// Main entry point for the AI agent. Receives user messages and orchestrates
/// the Supervisor → Confidence Scorer → Guardrail → Execution pipeline.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly OrchestratorService _orchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(OrchestratorService orchestrator, ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        _logger.LogInformation("Chat request from user {UserId}: {Message}", request.UserId, request.Message);

        var response = await _orchestrator.ProcessAsync(request);
        return Ok(response);
    }

    [HttpPost("approve/{sessionId}")]
    public async Task<ActionResult<ChatResponse>> Approve(string sessionId)
    {
        _logger.LogInformation("Approval received for session {SessionId}", sessionId);
        var response = await _orchestrator.ApproveActionAsync(sessionId);
        return Ok(response);
    }

    [HttpPost("reject/{sessionId}")]
    public ActionResult<ChatResponse> Reject(string sessionId)
    {
        _logger.LogInformation("Rejection received for session {SessionId}", sessionId);
        return Ok(new ChatResponse
        {
            SessionId = sessionId,
            Message = "Action has been rejected. The issue has been escalated to a human agent.",
            ActionTaken = "ticket_created"
        });
    }

    [HttpPost("feedback")]
    public ActionResult SubmitFeedback([FromBody] FeedbackRequest feedback)
    {
        _logger.LogInformation("Feedback for session {SessionId}: {Rating}", feedback.SessionId, feedback.IsPositive ? "Positive" : "Negative");
        _orchestrator.RecordFeedback(feedback.SessionId, feedback.IsPositive, feedback.Comment);
        return Ok(new { message = "Feedback recorded. Thank you!" });
    }
}

public class FeedbackRequest
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsPositive { get; set; }
    public string? Comment { get; set; }
}
