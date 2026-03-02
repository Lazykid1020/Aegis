using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// MVP Orchestrator. Simply retrieves context from Vector DB and passes it to the BaseAgent.
/// </summary>
public class OrchestratorService
{
    private readonly BaseAgent _agent;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(BaseAgent agent, ILogger<OrchestratorService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // 1. Mock Vector DB Context Retrieval
        _logger.LogInformation("[Session {Session}] Retrieving context from Vector DB...", sessionId);
        var mockVectorDbContext = GetMockVectorDbContext(request.Message);

        // 2. Process message through the Base Agent
        _logger.LogInformation("[Session {Session}] Passing to Base Agent...", sessionId);
        var responseText = await _agent.ProcessMessageAsync(sessionId, request.Message, mockVectorDbContext);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = responseText,
            ActionTaken = "none" // The agent actually uses the ticket tool natively, so we just return the text.
        };
    }

    // Mocking the Vector DB Context retrieval for the MVP
    private string GetMockVectorDbContext(string query)
    {
        query = query.ToLower();
        if (query.Contains("vpn"))
            return "Article: VPN Access. Staff level employees cannot access the production VPN. Only Admins can. Contractor access must be requested via ticket.";
        
        if (query.Contains("password") || query.Contains("locked"))
            return "SOP: Account Recovery. For locked accounts, users must submit a ticket if the Self-Service portal fails. For password resets, verify identity via ticket.";
        
        return "General IT Knowledge: If the user describes a hardware issue or critical server crash, always use the create_support_ticket tool with Urgency=High or Critical.";
    }

    // Unused in MVP, just here so the controller doesn't break if it has these routes
    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        await Task.CompletedTask;
        return new ChatResponse { SessionId = sessionId, Message = "Approvals not supported in MVP." };
    }

    public async Task<ChatResponse> RejectActionAsync(string sessionId)
    {
        await Task.CompletedTask;
        return new ChatResponse { SessionId = sessionId, Message = "Approvals not supported in MVP." };
    }

    public void RecordFeedback(string sessionId, bool isPositive, string? comment)
    {
        // No-op for MVP
    }
}
