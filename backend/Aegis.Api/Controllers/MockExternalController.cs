using Microsoft.AspNetCore.Mvc;
using Aegis.Api.Models;

namespace Aegis.Api.Controllers;

/// <summary>
/// Mock controller simulating external corporate systems (ServiceNow, Active Directory, etc.)
/// In production, these would be replaced by real API integrations.
/// </summary>
[ApiController]
[Route("api/mock")]
public class MockExternalController : ControllerBase
{
    private static readonly List<TicketRecord> _tickets = new();

    // ── ServiceNow Mock ──────────────────────────────────────

    [HttpPost("servicenow/ticket")]
    public IActionResult CreateTicket([FromBody] TicketRecord ticket)
    {
        ticket.Id = $"INC{Random.Shared.Next(100000, 999999)}";
        ticket.CreatedAt = DateTime.UtcNow;
        ticket.Status = "Open";
        _tickets.Add(ticket);

        return Ok(new
        {
            success = true,
            ticketId = ticket.Id,
            message = $"Ticket {ticket.Id} created in ServiceNow.",
            ticket
        });
    }

    [HttpGet("servicenow/tickets")]
    public IActionResult GetTickets()
    {
        return Ok(_tickets);
    }

    [HttpPatch("servicenow/ticket/{ticketId}/resolve")]
    public IActionResult ResolveTicket(string ticketId, [FromBody] TicketResolutionRequest resolution)
    {
        var ticket = _tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null) return NotFound(new { message = $"Ticket {ticketId} not found." });

        ticket.Status = "Resolved";
        ticket.ResolutionNotes = resolution.Notes;
        ticket.ResolvedByTool = resolution.ResolvedBy;
        ticket.ResolvedAt = DateTime.UtcNow;

        return Ok(new
        {
            success = true,
            message = $"Ticket {ticketId} resolved.",
            ticket
        });
    }

    // ── Active Directory Mock ────────────────────────────────

    [HttpPost("activedirectory/unlock")]
    public IActionResult UnlockAccount([FromBody] UserActionRequest request)
    {
        return Ok(new
        {
            success = true,
            message = $"Account for user '{request.Username}' has been unlocked.",
            action = "account_unlock",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("activedirectory/reset-password")]
    public IActionResult ResetPassword([FromBody] UserActionRequest request)
    {
        var tempPassword = $"Temp{Random.Shared.Next(1000, 9999)}!";
        return Ok(new
        {
            success = true,
            message = $"Password for user '{request.Username}' has been reset.",
            temporaryPassword = tempPassword,
            action = "password_reset",
            timestamp = DateTime.UtcNow
        });
    }

    // ── HR System Mock ───────────────────────────────────────

    [HttpGet("hr/employee/{employeeId}")]
    public IActionResult GetEmployee(string employeeId)
    {
        return Ok(new
        {
            employeeId,
            name = "John Doe",
            department = "Engineering",
            role = "Software Engineer",
            accessLevel = "Standard",
            manager = "Jane Smith"
        });
    }
}

// ── Supporting DTOs ──────────────────────────────────────────

public class TicketResolutionRequest
{
    public string Notes { get; set; } = string.Empty;
    public string ResolvedBy { get; set; } = "HumanAgent";
}

public class UserActionRequest
{
    public string Username { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
