namespace Aegis.Api.Models;

/// <summary>
/// Risk assessment produced by the Guardrail Agent.
/// </summary>
public class RiskAssessment
{
    public string RiskLevel { get; set; } = "Low"; // "Low", "Medium", "High"
    public int RiskScore { get; set; } // 0-100
    public string Reasoning { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public string? PolicyViolation { get; set; }
}
