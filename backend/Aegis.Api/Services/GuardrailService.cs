using Aegis.Api.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

#pragma warning disable SKEXP0070

namespace Aegis.Api.Services;

/// <summary>
/// LLM-powered guardrail that validates user inputs and agent outputs
/// against configurable platform/tenant policies. No keyword blocklists —
/// the LLM understands context and intent.
/// </summary>
public class GuardrailService
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<GuardrailService> _logger;
    private readonly string _scope;
    private readonly List<PolicyRule> _inputPolicies;
    private readonly List<PolicyRule> _outputPolicies;

    private record PolicyRule(string Id, string Description);

    public GuardrailService(Kernel kernel, ILogger<GuardrailService> logger)
    {
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;

        // Load policies from JSON config
        var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "guardrail-policies.json");
        if (!File.Exists(configPath))
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "guardrail-policies.json");

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);

        _scope = doc.RootElement.GetProperty("scope").GetString() ?? "";
        _inputPolicies = new List<PolicyRule>();
        _outputPolicies = new List<PolicyRule>();

        foreach (var policy in doc.RootElement.GetProperty("policies").EnumerateArray())
        {
            var id = policy.GetProperty("id").GetString() ?? "";
            var type = policy.GetProperty("type").GetString() ?? "";
            var desc = policy.GetProperty("description").GetString() ?? "";

            if (type == "input")
                _inputPolicies.Add(new PolicyRule(id, desc));
            else
                _outputPolicies.Add(new PolicyRule(id, desc));
        }

        _logger.LogInformation("GuardrailService loaded: {InputCount} input policies, {OutputCount} output policies.",
            _inputPolicies.Count, _outputPolicies.Count);
    }

    /// <summary>
    /// Validates a user message against scope and input policies.
    /// Returns quickly if no input policies exist.
    /// </summary>
    public async Task<GuardrailResult> ValidateInputAsync(string userMessage)
    {
        var policyList = string.Join("\n", _inputPolicies.Select(p => $"- [{p.Id}] {p.Description}"));

        var systemPrompt = $@"You are a strict policy validator. Your job is to check if a user message violates any of the following rules.

## SCOPE
{_scope}

## INPUT POLICIES
{policyList}

## INSTRUCTIONS
Analyze the user message. If it violates the SCOPE or any INPUT POLICY, respond with EXACTLY this JSON (no other text):
{{""allowed"": false, ""policyId"": ""<id or 'scope'>"", ""reason"": ""<brief reason>"", ""correctedResponse"": ""<a polite, helpful redirect message to give the user>""}}

If the message is fine, respond with EXACTLY:
{{""allowed"": true}}

IMPORTANT: Normal greetings like 'hello' or 'how are you' are ALLOWED — they are not off-scope. Only block messages that are clearly asking for help OUTSIDE of issue management.";

        return await EvaluateAsync(systemPrompt, $"User message: \"{userMessage}\"", "input", userMessage);
    }

    /// <summary>
    /// Validates an agent response against output policies.
    /// </summary>
    public async Task<GuardrailResult> ValidateOutputAsync(string agentResponse, string userMessage)
    {
        if (_outputPolicies.Count == 0)
            return new GuardrailResult { Allowed = true };

        var policyList = string.Join("\n", _outputPolicies.Select(p => $"- [{p.Id}] {p.Description}"));

        var systemPrompt = $@"You are a strict policy validator. Your job is to check if an AI assistant's response violates any of the following rules.

## OUTPUT POLICIES
{policyList}

## INSTRUCTIONS
Analyze the agent's response. If it violates any OUTPUT POLICY, respond with EXACTLY this JSON (no other text):
{{""allowed"": false, ""policyId"": ""<policy id>"", ""reason"": ""<brief reason>"", ""correctedResponse"": ""<the full corrected response with the violation removed or fixed>""}}

If the response is fine, respond with EXACTLY:
{{""allowed"": true}}

The corrected response should be natural and helpful — just remove or rephrase the violating part.";

        var content = $"User asked: \"{userMessage}\"\nAgent responded: \"{agentResponse}\"";
        return await EvaluateAsync(systemPrompt, content, "output", agentResponse);
    }

    private async Task<GuardrailResult> EvaluateAsync(string systemPrompt, string content, string direction, string original)
    {
        try
        {
            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(content);

            var result = await _chatService.GetChatMessageContentAsync(history);
            var raw = result.Content?.Trim() ?? "";

            // Try to parse JSON from the response (handle markdown code blocks)
            var jsonStr = raw;
            if (raw.Contains("```"))
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start >= 0 && end > start)
                    jsonStr = raw.Substring(start, end - start + 1);
            }

            using var doc = JsonDocument.Parse(jsonStr);
            var allowed = doc.RootElement.GetProperty("allowed").GetBoolean();

            if (allowed)
            {
                _logger.LogDebug("[Guardrail-{Direction}] ✅ Allowed", direction);
                return new GuardrailResult { Allowed = true };
            }

            var policyId = doc.RootElement.TryGetProperty("policyId", out var pid) ? pid.GetString() : "unknown";
            var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : "Policy violation";
            var corrected = doc.RootElement.TryGetProperty("correctedResponse", out var c) ? c.GetString() : null;

            _logger.LogWarning("[Guardrail-{Direction}] ⛔ Blocked | Policy: {Policy} | Reason: {Reason}",
                direction, policyId, reason);

            return new GuardrailResult
            {
                Allowed = false,
                ViolatedPolicyId = policyId,
                Reason = reason,
                CorrectedResponse = corrected ?? "I'm here to help with issue management. How can I assist you?"
            };
        }
        catch (Exception ex)
        {
            // If guardrail fails, fail OPEN (allow the message through)
            _logger.LogWarning(ex, "[Guardrail-{Direction}] Failed to evaluate — allowing through.", direction);
            return new GuardrailResult { Allowed = true };
        }
    }
}

#pragma warning restore SKEXP0070
