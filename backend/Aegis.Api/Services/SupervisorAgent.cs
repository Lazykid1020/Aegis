using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

namespace Aegis.Api.Services;

public class SupervisorResult
{
    public bool IsReadyForAction { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
}

/// <summary>
/// The Supervisor Agent: understands user intent, gathers info via conversation, 
/// and executes tools via Semantic Kernel when approved.
/// </summary>
public class SupervisorAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SupervisorAgent> _logger;

    // In-memory conversation history per session
    private static readonly Dictionary<string, ChatHistory> _sessions = new();

    public SupervisorAgent(Kernel kernel, ILogger<SupervisorAgent> logger)
    {
        _kernel = kernel;
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    /// <summary>
    /// Chats with the user to gather necessary parameters. Returns IsReadyForAction=true 
    /// only when all info is collected.
    /// </summary>
    public async Task<SupervisorResult> AnalyzeIntentAsync(string sessionId, string userMessage)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
        {
            var systemPrompt = @"You are Aegis, a friendly enterprise IT support agent.
Your goal is to understand the user's issue and gather ALL required parameters before taking action.
Required parameters per action:
- Password Reset: Needs the EXACT username.
- Account Unlock: Needs the EXACT username.
- Other IT issues (e.g. server crash, software install): Needs a clear description of the problem.

If you DO NOT have all the required info, ask the user for it naturally and politely in 1-2 sentences. Do NOT take action yet.
If you DO have all the required info, you must output exactly this format and nothing else:
[INTENT_READY] <concise description of the fully parameterized intent, e.g. 'Reset password for user jdoe' or 'Grant admin root access to contractor1'>" ;

            history = new ChatHistory(systemPrompt);
            _sessions[sessionId] = history;
        }

        history.AddUserMessage(userMessage);

        var result = await _chatService.GetChatMessageContentAsync(history);
        var responseText = result.Content ?? string.Empty;

        // Add assistant response to history
        history.AddAssistantMessage(responseText);

        _logger.LogInformation("[Session {Session}] Supervisor raw response: {Response}", sessionId, responseText);

        if (responseText.Contains("[INTENT_READY]"))
        {
            var intent = responseText.Replace("[INTENT_READY]", "").Trim();
            // Remove the INTENT_READY message from history so it doesn't confuse future turns,
            // replace it with an acknowledgement.
            history.RemoveAt(history.Count - 1);
            history.AddAssistantMessage($"I have gathered the intent: {intent}. Processing action...");

            return new SupervisorResult
            {
                IsReadyForAction = true,
                Intent = intent,
                Message = "Information gathered. Processing action..."
            };
        }

        return new SupervisorResult
        {
            IsReadyForAction = false,
            Message = responseText,
            Intent = string.Empty
        };
    }

    /// <summary>
    /// Executes the appropriate tool/plugin based on the fully parameterized intent.
    /// SK's auto function-calling will pick the right plugin.
    /// </summary>
    public async Task<string> ExecuteToolAsync(string intent)
    {
        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var prompt = @"You are the Aegis Execution Layer. 
Based exactly on the following intent, execute the appropriate tool function. 
Only summarize the result of the tool execution in a friendly way.

Intent: {{$input}}";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt,
                new(settings) { ["input"] = intent });
            return result.GetValue<string>() ?? "Action completed but no details returned.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool for intent: {Intent}", intent);
            return $"Failed to execute the action: {ex.Message}";
        }
    }
}

#pragma warning restore SKEXP0070
