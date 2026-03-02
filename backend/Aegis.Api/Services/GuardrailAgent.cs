using Microsoft.SemanticKernel;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// The Guardrail Agent: assesses the risk of a proposed action by checking
/// against security policies stored in the Experience Engine (Vector DB).
/// </summary>
public class GuardrailAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<GuardrailAgent> _logger;

    // Simulated security policies (in production, these come from the Vector DB)
    private static readonly List<SecurityPolicy> _policies = new()
    {
        new("NEVER grant admin or elevated privileges without VP-level approval.", "High", "admin|elevated|root|superuser"),
        new("Contractors must NOT be added to internal VPN or financial systems.", "High", "contractor.*vpn|contractor.*finance"),
        new("Password resets for service accounts require Security team approval.", "Medium", "service.account.*password"),
        new("Software installations must be from the approved software catalog.", "Medium", "install.*software|install.*app"),
        new("Data export from production databases is forbidden without DLP review.", "High", "export.*production|download.*database"),
    };

    public GuardrailAgent(Kernel kernel, ILogger<GuardrailAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<RiskAssessment> AssessRiskAsync(string intent, string originalMessage)
    {
        // Phase 1: Check against hardcoded security policies (fast)
        var policyMatch = CheckPolicies(intent + " " + originalMessage);
        if (policyMatch != null)
        {
            return new RiskAssessment
            {
                RiskLevel = policyMatch.Severity,
                RiskScore = policyMatch.Severity == "High" ? 95 : 65,
                Reasoning = policyMatch.Rule,
                IsBlocked = policyMatch.Severity == "High",
                PolicyViolation = policyMatch.Rule
            };
        }

        // Phase 2: Ask LLM for nuanced risk assessment
        var prompt = @"You are a corporate security risk assessor. Evaluate the following IT support action
for security risks. Consider:
- Does this involve sensitive data or systems?
- Could this action be exploited?
- Does this require verification of the requester's identity?

Action/Intent: {{$input}}

Respond in this exact JSON format (no markdown, no code blocks):
{""riskLevel"": ""Low|Medium|High"", ""riskScore"": 0-100, ""reasoning"": ""brief explanation""}";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, new() { ["input"] = intent });
            var json = result.GetValue<string>() ?? "";
            
            // Simple JSON parsing (in production, use System.Text.Json properly)
            var assessment = System.Text.Json.JsonSerializer.Deserialize<RiskAssessmentResponse>(json);
            if (assessment != null)
            {
                return new RiskAssessment
                {
                    RiskLevel = assessment.RiskLevel ?? "Medium",
                    RiskScore = assessment.RiskScore,
                    Reasoning = assessment.Reasoning ?? "LLM assessment",
                    IsBlocked = assessment.RiskScore >= 90
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM risk assessment, defaulting to Medium.");
        }

        // Default: Medium risk if we can't determine
        return new RiskAssessment
        {
            RiskLevel = "Medium",
            RiskScore = 50,
            Reasoning = "Could not fully assess risk, defaulting to medium.",
            IsBlocked = false
        };
    }

    private SecurityPolicy? CheckPolicies(string text)
    {
        var lowerText = text.ToLowerInvariant();
        foreach (var policy in _policies)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(lowerText, policy.Pattern))
            {
                _logger.LogWarning("Policy match: {Rule}", policy.Rule);
                return policy;
            }
        }
        return null;
    }
}

// Internal models
internal record SecurityPolicy(string Rule, string Severity, string Pattern);

internal class RiskAssessmentResponse
{
    public string? RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public string? Reasoning { get; set; }
}
