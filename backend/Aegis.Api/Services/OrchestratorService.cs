using Aegis.Api.Models;
using Aegis.Api.Plugins;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aegis.Api.Services;

/// <summary>
/// Thin orchestrator that owns the confidence score per session.
/// Fetches RAG context, passes the current confidence to the agent,
/// parses confidence deltas, and checks the ITOperationsPlugin for
/// pending ticket approvals (no more regex signal parsing for tickets).
/// </summary>
public class OrchestratorService
{
    private readonly BaseAgent _agent;
    private readonly KnowledgeBaseService _knowledgeBase;
    private readonly ITOperationsPlugin _ticketPlugin;
    private readonly ChatLogger _chatLogger;
    private readonly ILogger<OrchestratorService> _logger;

    // Server-side confidence tracking per session
    private static readonly Dictionary<string, double> _sessionConfidence = new();

    private const double InitialConfidence = 1.0;
    private const double EscalationThreshold = 0.4;

    private static readonly Regex ConfidenceDeltaRegex = new(
        @":::CONFIDENCE_DELTA:::\s*(\{.*?\})\s*:::END_DELTA:::",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public OrchestratorService(
        BaseAgent agent,
        KnowledgeBaseService knowledgeBase,
        ITOperationsPlugin ticketPlugin,
        ChatLogger chatLogger,
        ILogger<OrchestratorService> logger)
    {
        _agent = agent;
        _knowledgeBase = knowledgeBase;
        _ticketPlugin = ticketPlugin;
        _chatLogger = chatLogger;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // 1. Get or initialize confidence for this session
        if (!_sessionConfidence.ContainsKey(sessionId))
            _sessionConfidence[sessionId] = InitialConfidence;

        var currentConfidence = _sessionConfidence[sessionId];

        // 2. Fetch RAG context
        var ragContext = await _knowledgeBase.SearchAsync(request.Message);

        // 3. Set session ID on the plugin so tool calls know which session they belong to
        ITOperationsPlugin.CurrentSessionId = sessionId;

        // 4. Prepend confidence to the user message
        var enrichedMessage = $"[CONFIDENCE: {currentConfidence:F2}] {request.Message}";

        _logger.LogInformation("[Session {Session}] Confidence: {Confidence:F2} | Sending to agent.", sessionId, currentConfidence);

        // 5. Let the agent handle the conversation
        var rawResponse = await _agent.ProcessMessageAsync(sessionId, enrichedMessage, ragContext);

        // 6. Parse and apply confidence delta
        var newConfidence = ApplyConfidenceDelta(sessionId, rawResponse, currentConfidence);

        // 7. Strip delta signal from user-facing output
        var cleanResponse = ConfidenceDeltaRegex.Replace(rawResponse, "").Trim();

        // 8. Log the conversation turn
        await _chatLogger.LogTurnAsync(sessionId, request.UserId, request.Message, cleanResponse, newConfidence);

        // 9. Check if the tool stored a pending ticket (tool-based approval gate)
        if (ITOperationsPlugin.HasPendingTicket(sessionId))
        {
            var ticket = ITOperationsPlugin.GetPendingTicket(sessionId)!;
            _logger.LogInformation("[Session {Session}] Tool created pending ticket: {Title}", sessionId, ticket.Title);

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = cleanResponse,
                ActionTaken = "awaiting_approval",
                RequiresApproval = true,
                ProposedTicket = ticket
            };
        }

        // 10. Check if confidence crossed the threshold — force ticket escalation
        if (newConfidence < EscalationThreshold && !ITOperationsPlugin.HasPendingTicket(sessionId))
        {
            _logger.LogInformation("[Session {Session}] Confidence {Confidence:F2} below threshold. Forcing escalation.",
                sessionId, newConfidence);

            ITOperationsPlugin.CurrentSessionId = sessionId;

            var escalationInstruction =
                "Confidence is critically low. You MUST now call the create_support_ticket tool " +
                "to propose a ticket for this issue. Summarize the issue and what was discussed.";

            var escalationResponse = await _agent.SendSystemInstructionAsync(sessionId, escalationInstruction, ragContext);
            var escalationClean = ConfidenceDeltaRegex.Replace(escalationResponse, "").Trim();

            await _chatLogger.LogTurnAsync(sessionId, "system", "[ESCALATION_FORCED]", escalationClean, newConfidence);

            if (ITOperationsPlugin.HasPendingTicket(sessionId))
            {
                var ticket = ITOperationsPlugin.GetPendingTicket(sessionId)!;
                return new ChatResponse
                {
                    SessionId = sessionId,
                    Message = escalationClean,
                    ActionTaken = "awaiting_approval",
                    RequiresApproval = true,
                    ProposedTicket = ticket
                };
            }

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = escalationClean,
                ActionTaken = "none"
            };
        }

        // 11. Normal response
        return new ChatResponse
        {
            SessionId = sessionId,
            Message = cleanResponse,
            ActionTaken = "none"
        };
    }

    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        _logger.LogInformation("[Session {Session}] Ticket approved by user.", sessionId);

        if (ITOperationsPlugin.HasPendingTicket(sessionId))
        {
            // Actually create the ticket via the mock API
            var result = await _ticketPlugin.ConfirmPendingTicketAsync(sessionId);

            // Reset confidence after ticket creation
            _sessionConfidence[sessionId] = InitialConfidence;

            // Tell the agent the ticket was created so it can respond naturally
            var ragContext = await _knowledgeBase.SearchAsync("");
            var responseText = await _agent.SendSystemInstructionAsync(
                sessionId,
                $"The user approved the ticket and it has been created. {result} " +
                "Briefly confirm to the user and ask if there's anything else you can help with.",
                ragContext);

            responseText = ConfidenceDeltaRegex.Replace(responseText, "").Trim();
            await _chatLogger.LogTurnAsync(sessionId, "system", "[TICKET_APPROVED]", responseText, InitialConfidence);

            return new ChatResponse
            {
                SessionId = sessionId,
                Message = responseText,
                ActionTaken = "ticket_created"
            };
        }

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = "No pending ticket request found for this session.",
            ActionTaken = "none"
        };
    }

    public async Task<ChatResponse> RejectActionAsync(string sessionId)
    {
        _logger.LogInformation("[Session {Session}] Ticket rejected by user.", sessionId);

        ITOperationsPlugin.ClearPendingTicket(sessionId);

        // Bump confidence slightly after rejection
        if (_sessionConfidence.ContainsKey(sessionId))
            _sessionConfidence[sessionId] = Math.Min(_sessionConfidence[sessionId] + 0.15, 1.0);

        var ragContext = await _knowledgeBase.SearchAsync("");
        var responseText = await _agent.SendSystemInstructionAsync(
            sessionId,
            "The user rejected the ticket. Acknowledge briefly and ask if there's anything else you can help with.",
            ragContext);

        responseText = ConfidenceDeltaRegex.Replace(responseText, "").Trim();
        await _chatLogger.LogTurnAsync(sessionId, "system", "[TICKET_REJECTED]", responseText, _sessionConfidence.GetValueOrDefault(sessionId, InitialConfidence));

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = responseText,
            ActionTaken = "none"
        };
    }

    private double ApplyConfidenceDelta(string sessionId, string rawResponse, double currentConfidence)
    {
        var deltaMatch = ConfidenceDeltaRegex.Match(rawResponse);
        if (!deltaMatch.Success) return currentConfidence;

        try
        {
            var deltaJson = deltaMatch.Groups[1].Value;
            using var doc = JsonDocument.Parse(deltaJson);
            var delta = doc.RootElement.GetProperty("delta").GetDouble();
            var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "n/a";

            delta = Math.Clamp(delta, -1.0, 0.5);
            var newConfidence = Math.Clamp(currentConfidence + delta, 0.0, 1.0);
            _sessionConfidence[sessionId] = newConfidence;

            _logger.LogInformation("[Session {Session}] Delta: {Delta:+0.00;-0.00} → Confidence: {Old:F2} → {New:F2} | Reason: {Reason}",
                sessionId, delta, currentConfidence, newConfidence, reason);

            return newConfidence;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Session {Session}] Failed to parse confidence delta.", sessionId);
            return currentConfidence;
        }
    }
}
