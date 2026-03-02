using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// MVP Orchestrator. Simply retrieves context from Vector DB and passes it to the BaseAgent.
/// </summary>
public class OrchestratorService
{
    private readonly BaseAgent _agent;
    private readonly KnowledgeBaseService _knowledgeBase;
    private readonly ILogger<OrchestratorService> _logger;

    public OrchestratorService(BaseAgent agent, KnowledgeBaseService knowledgeBase, ILogger<OrchestratorService> logger)
    {
        _agent = agent;
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // 1. Context Retrieval via Volatile Vector DB (RAG)
        _logger.LogInformation("[Session {Session}] Retrieving RAG context from Volatile DB...", sessionId);
        var vectorDbContext = await _knowledgeBase.SearchAsync(request.Message);

        // 2. Process message through the Base Agent
        _logger.LogInformation("[Session {Session}] Passing to Base Agent. Context: \n{Context}", sessionId, vectorDbContext);
        var responseText = await _agent.ProcessMessageAsync(sessionId, request.Message, vectorDbContext);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = responseText,
            ActionTaken = "none" // The agent actually uses the ticket tool natively, so we just return the text.
        };
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
