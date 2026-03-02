using Microsoft.AspNetCore.Mvc;
using Aegis.Api.Models;
using Aegis.Api.Services;

namespace Aegis.Api.Controllers;

/// <summary>
/// Main entry point for the AI agent. Receives user messages and routes them
/// through the Orchestrator → BaseAgent pipeline.
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
        _logger.LogInformation("Ticket approval received for session {SessionId}", sessionId);
        var response = await _orchestrator.ApproveActionAsync(sessionId);
        return Ok(response);
    }

    [HttpPost("reject/{sessionId}")]
    public async Task<ActionResult<ChatResponse>> Reject(string sessionId)
    {
        _logger.LogInformation("Ticket rejection received for session {SessionId}", sessionId);
        var response = await _orchestrator.RejectActionAsync(sessionId);
        return Ok(response);
    }
}
