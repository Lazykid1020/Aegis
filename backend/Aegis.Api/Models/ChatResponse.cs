namespace Aegis.Api.Models;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public ConfidenceResult? Confidence { get; set; }
    public RiskAssessment? Risk { get; set; }
    public string ActionTaken { get; set; } = "none"; // "auto_remediated", "awaiting_approval", "ticket_created", "solution_provided", "none"
    public string? TicketId { get; set; }
    public bool RequiresApproval { get; set; }
}
