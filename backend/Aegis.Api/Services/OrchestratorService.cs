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

    private static readonly Dictionary<string, string> _pendingTickets = new();

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
        var context = await _knowledgeBase.SearchAsync(request.Message);

        // 2. Evaluate Dynamic Session Confidence via LLM
        _logger.LogInformation("[Session {Session}] Evaluating Confidence...", sessionId);
        var eval = await _agent.EvaluateConfidenceAsync(sessionId, request.Message, context);
        _logger.LogInformation("[Session {Session}] Confidence Score: {Score}%. Reasoning: {Reasoning}", sessionId, eval.Score, eval.Reasoning);

        // 3. Confidence Based Routing
        const double ConfidenceThreshold = 75.0;

        if (eval.Score >= ConfidenceThreshold)
        {
            // Tier 1: High Confidence -> Answer immediately based on Knowledge Base
            _logger.LogInformation("[Session {Session}] High Confidence. Routing to Base Agent.", sessionId);
            var responseText = await _agent.ProcessMessageAsync(sessionId, request.Message, context);

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = $"{responseText}\n\n*(Confidence: {eval.Score:F1}% - {eval.Reasoning})*",
                ActionTaken = "action_info" // Tells UI to show feedback buttons
            };
        }
        else
        {
            // Tier 2: Low Confidence -> Ask to raise a ticket
            _logger.LogWarning("[Session {Session}] Low Confidence ({Score}%). Requesting Ticket Approval.", sessionId, eval.Score);
            _pendingTickets[sessionId] = request.Message;

            // Notice we feed the reasoning back to the user to explain WHY we're asking for a ticket
            return new ChatResponse
            {
                SessionId = sessionId,
                Message = $"I am only {eval.Score:F1}% confident I can safely solve this based on my knowledge base. ({eval.Reasoning})\n\nWould you like me to skip troubleshooting and raise a Priority IT Support Ticket for human assistance right now?",
                ActionTaken = "awaiting_approval",
                RequiresApproval = true
            };
        }
    }

    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        _logger.LogInformation("Approval received to raise ticket for session {SessionId}.", sessionId);
        
        if (_pendingTickets.TryGetValue(sessionId, out var issueDescription))
        {
            _pendingTickets.Remove(sessionId);
            
            // Send a hidden prompt to the agent to forcefully trigger the ticket creation tool
            var prompt = $"The user explicitly approved creating a support ticket for this issue: '{issueDescription}'. Please use your create_support_ticket tool now.";
            var responseText = await _agent.ProcessMessageAsync(sessionId, prompt, "General IT Knowledge: Unresolvable issues require tickets.");

            return new ChatResponse 
            { 
                SessionId = sessionId, 
                Message = responseText,
                ActionTaken = "ticket_created"
            };
        }

        return new ChatResponse { SessionId = sessionId, Message = "No pending ticket request found." };
    }

    public Task<ChatResponse> RejectActionAsync(string sessionId)
    {
        _logger.LogInformation("Rejection received for ticket creation in session {SessionId}.", sessionId);
        if (_pendingTickets.ContainsKey(sessionId))
        {
            _pendingTickets.Remove(sessionId);
        }

        return Task.FromResult(new ChatResponse
        {
            SessionId = sessionId,
            Message = "Understood. I have not created a ticket. How else can I help?",
            ActionTaken = "none"
        });
    }

    public void RecordFeedback(string sessionId, bool isPositive, string? comment)
    {
        // For the new conversational feedback model, the feedback is sent as a chat message. 
        // This endpoint is no longer strictly necessary but kept for backwards compatibility with UI if needed.
        _logger.LogInformation("Feedback endpoint hit for Session {Session}. (Ignored in favor of conversational feedback).", sessionId);
    }
}
