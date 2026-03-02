namespace Aegis.Api.Models;

/// <summary>
/// Represents a historical or new ticket stored in the system.
/// Used both for the Vector DB Experience Engine and for new ticket creation.
/// </summary>
public class TicketRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "IT", "HR", "Facilities", "Security"
    public string Status { get; set; } = "Open"; // "Open", "InProgress", "Resolved", "Escalated"
    public string? ResolutionNotes { get; set; }
    public string? ResolvedByTool { get; set; } // e.g. "PasswordResetPlugin", "BrowserAgent", "HumanAgent"
    public string Urgency { get; set; } = "Medium"; // "Low", "Medium", "High", "Critical"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string ReportedBy { get; set; } = string.Empty;
}
