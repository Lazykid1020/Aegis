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

    // In-memory state tracking for the Hackathon POC
    private static readonly Dictionary<string, string> _sessionTopArticle = new();
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
        var ragResult = await _knowledgeBase.SearchAndScoreAsync(request.Message);

        _logger.LogInformation("[Session {Session}] Confidence Score: {Score}%. Top Article: {Article}", sessionId, ragResult.ConfidenceScore, ragResult.TopArticleId);

        // Track the article used so we can attach feedback to it later
        if (ragResult.TopArticleId != null)
        {
            _sessionTopArticle[sessionId] = ragResult.TopArticleId;
        }

        // 2. Confidence Based Routing
        const double ConfidenceThreshold = 75.0;

        if (ragResult.ConfidenceScore >= ConfidenceThreshold)
        {
            // Tier 1: High Confidence -> Answer immediately based on Knowledge Base
            _logger.LogInformation("[Session {Session}] High Confidence. Routing to Base Agent.", sessionId);
            var responseText = await _agent.ProcessMessageAsync(sessionId, request.Message, ragResult.Context);

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = $"{responseText}\n\n*(Confidence: {ragResult.ConfidenceScore:F1}%)*",
                ActionTaken = "action_info" // Tells UI to show feedback buttons
            };
        }
        else
        {
            // Tier 2: Low Confidence -> Ask to raise a ticket
            _logger.LogWarning("[Session {Session}] Low Confidence ({Score}%). Requesting Ticket Approval.", sessionId, ragResult.ConfidenceScore);
            _pendingTickets[sessionId] = request.Message;

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = $"I am only {ragResult.ConfidenceScore:F1}% confident I safely can solve this based on the knowledge base. Would you like me to raise a Priority IT Support Ticket for human assistance?",
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
        if (_sessionTopArticle.TryGetValue(sessionId, out var articleId))
        {
            _logger.LogInformation("Routing feedback for Session {Session} to Article {ArticleId}. Positive: {IsPositive}", sessionId, articleId, isPositive);
            _knowledgeBase.AdjustConfidence(articleId, isPositive);
        }
        else
        {
            _logger.LogWarning("Feedback received for Session {Session} but no associated article was found in memory.", sessionId);
        }
    }
}
