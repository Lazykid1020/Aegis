namespace Aegis.Api.Models;

public class GuardrailResult
{
    public bool Allowed { get; set; } = true;
    public string? ViolatedPolicyId { get; set; }
    public string? Reason { get; set; }
    public string? CorrectedResponse { get; set; }
}
