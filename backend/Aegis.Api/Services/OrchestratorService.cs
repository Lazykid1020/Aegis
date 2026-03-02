using Aegis.Api.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aegis.Api.Services;

/// <summary>
/// Thin orchestrator. Fetches RAG context, passes messages to the BaseAgent,
/// and parses the agent's response for ticket escalation signals.
/// </summary>
public class OrchestratorService
{
    private readonly BaseAgent _agent;
    private readonly KnowledgeBaseService _knowledgeBase;
    private readonly ILogger<OrchestratorService> _logger;

    // Stores pending ticket details per session so we can create them on approval
    private static readonly Dictionary<string, TicketPreview> _pendingTickets = new();

    private static readonly Regex TicketSignalRegex = new(
        @":::TICKET_SIGNAL:::\s*(\{.*?\})\s*:::END_SIGNAL:::",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public OrchestratorService(BaseAgent agent, KnowledgeBaseService knowledgeBase, ILogger<OrchestratorService> logger)
    {
        _agent = agent;
        _knowledgeBase = knowledgeBase;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(ChatRequest request)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        // 1. Fetch RAG context
        var ragContext = await _knowledgeBase.SearchAsync(request.Message);
        _logger.LogInformation("[Session {Session}] RAG context retrieved. Passing to agent.", sessionId);

        // 2. Let the agent handle everything
        var rawResponse = await _agent.ProcessMessageAsync(sessionId, request.Message, ragContext);

        // 3. Check if the agent emitted a ticket escalation signal
        var signalMatch = TicketSignalRegex.Match(rawResponse);

        if (signalMatch.Success)
        {
            try
            {
                var json = signalMatch.Groups[1].Value;
                var ticket = JsonSerializer.Deserialize<TicketPreview>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (ticket != null && ticket.Action == "REQUEST_TICKET_APPROVAL")
                {
                    // Store for later approval
                    _pendingTickets[sessionId] = ticket;

                    // Strip the raw signal from the user-facing message
                    var cleanMessage = TicketSignalRegex.Replace(rawResponse, "").Trim();

                    _logger.LogInformation("[Session {Session}] Agent requested ticket approval: {Title}", sessionId, ticket.Title);

                    return new ChatResponse
                    {
                        SessionId = sessionId,
                        Message = cleanMessage,
                        ActionTaken = "awaiting_approval",
                        RequiresApproval = true,
                        ProposedTicket = ticket
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[Session {Session}] Failed to parse ticket signal JSON.", sessionId);
            }
        }

        // Normal conversational response
        return new ChatResponse
        {
            SessionId = sessionId,
            Message = rawResponse,
            ActionTaken = "none"
        };
    }

    public async Task<ChatResponse> ApproveActionAsync(string sessionId)
    {
        _logger.LogInformation("[Session {Session}] Ticket creation approved by user.", sessionId);

        if (_pendingTickets.TryGetValue(sessionId, out var ticket))
        {
            _pendingTickets.Remove(sessionId);

            var instruction = $"The user has APPROVED creating the support ticket. " +
                              $"Please now use the create_support_ticket tool with these details: " +
                              $"Title: '{ticket.Title}', Description: '{ticket.Description}', " +
                              $"Category: '{ticket.Category}', Urgency: '{ticket.Urgency}'.";

            var ragContext = await _knowledgeBase.SearchAsync(ticket.Title ?? "");
            var responseText = await _agent.SendSystemInstructionAsync(sessionId, instruction, ragContext);

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
        _logger.LogInformation("[Session {Session}] Ticket creation rejected by user.", sessionId);

        if (_pendingTickets.ContainsKey(sessionId))
        {
            _pendingTickets.Remove(sessionId);
        }

        var ragContext = await _knowledgeBase.SearchAsync("");
        var responseText = await _agent.SendSystemInstructionAsync(
            sessionId,
            "The user has REJECTED the ticket creation. Acknowledge this and ask if there's anything else you can help with.",
            ragContext);

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = responseText,
            ActionTaken = "none"
        };
    }
}
