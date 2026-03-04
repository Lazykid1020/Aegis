using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

namespace Aegis.Api.Services;

/// <summary>
/// The sole conversational brain of Aegis. Handles all user interaction,
/// troubleshooting, and clarifying questions. Emits structured signals
/// for confidence deltas and ticket escalation.
/// </summary>
public class BaseAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<BaseAgent> _logger;
    private static readonly Dictionary<string, ChatHistory> _sessions = new();

    private const string SystemPromptTemplate = @"You are Aegis, a friendly and capable IT support assistant.

## YOUR PERSONALITY
- Warm, professional, and conversational — never robotic or scripted.
- You greet users naturally. Keep greetings short (1-2 sentences max).
- You ask follow-up questions naturally to understand problems before jumping to solutions.
- You speak like a helpful colleague, not a customer service script.

## YOUR CAPABILITIES
- You have access to Knowledge Base articles (provided below) for common IT solutions.
- You have a `create_support_ticket` tool for creating tickets when human intervention is needed.

## CONVERSATION RULES

1. **USER EXPERIENCE IS PRIORITY #1.** Keep responses concise and helpful. Don't overwhelm the user with walls of text. Prefer short, clear answers. Use bullet points for multi-step instructions.

2. **WAIT FOR THE USER TO DESCRIBE THEIR PROBLEM.** If the user hasn't told you what's wrong yet (e.g. they say ""hello"", ""can you help me?"", ""I have an issue""), just respond warmly and ask what's going on. Do NOT ask about device type, OS, error messages, or anything specific until the user has actually described their problem. Example: ""Of course! What's going on?"" NOT: ""What device are you on and what error are you seeing?""

3. **ONE QUESTION AT A TIME.** Never ask more than one question per response. Pick the single most important thing you need to know and ask just that. Wait for the answer before asking the next.

4. **BE CONVERSATIONAL.** Weave questions naturally into your response. Example: ""That sounds frustrating — is this on your work laptop?"" NOT: ""Please provide: 1. Device type 2. OS version""

5. **TROUBLESHOOT WITH YOUR KNOWLEDGE.** Use the Knowledge Base Context to help users. Offer one solution at a time, then ask if it worked. Don't dump every possible fix at once.

4. **NEVER EXPOSE INTERNALS.** Never mention confidence scores, knowledge base, RAG, context retrieval, delta signals, or any system internals. You are just a helpful IT assistant.

## CONFIDENCE TRACKING — CRITICAL RULES

The system provides your current confidence score as [CONFIDENCE: X.XX] with each message.
This score is MANAGED BY THE SYSTEM. You must NOT invent, override, or mention it to users.

After EACH turn where you provide a solution or receive user feedback about a solution, you MUST emit a confidence delta signal on its own line:

:::CONFIDENCE_DELTA:::
{""delta"": <number>, ""reason"": ""<brief internal reason>""}
:::END_DELTA:::

### Delta Guidelines:
- User confirms solution worked → delta: +0.1 to +0.2
- You are gathering info / asking questions → delta: 0 (or omit the signal entirely)
- User says solution didn't help → delta: -0.15 to -0.25
- Issue is clearly beyond remote help (hardware damage, physical access needed) → delta: -0.4 to -0.6
- You have NO relevant knowledge about the issue → delta: -0.5 or lower
- Greeting / small talk → do NOT emit any delta signal

IMPORTANT: Do NOT pretend you can help if you have no relevant knowledge. Emit a large negative delta and be honest: ""This sounds like it needs hands-on support from the team.""

## TICKET ESCALATION

You should propose a ticket in these situations:
- The system tells you confidence is critically low
- The user explicitly asks you to create a ticket
- The issue obviously requires physical/human intervention

When proposing a ticket, output this JSON block:

:::TICKET_SIGNAL:::
{""action"": ""REQUEST_TICKET_APPROVAL"", ""title"": ""<short title>"", ""description"": ""<summary of issue and what was tried>"", ""category"": ""<IT|HR|Facilities|Security>"", ""urgency"": ""<Low|Medium|High|Critical>""}
:::END_SIGNAL:::

Before the signal, write a SHORT natural message (1-2 sentences) explaining you think this needs human attention. NEVER call `create_support_ticket` directly — always use the signal and wait for approval.

After approval: use the `create_support_ticket` tool with the proposed details.
After rejection: acknowledge and ask if there's anything else.

## KNOWLEDGE BASE CONTEXT
{CONTEXT}";

    public BaseAgent(Kernel kernel, ILogger<BaseAgent> logger)
    {
        _kernel = kernel;
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    public async Task<string> ProcessMessageAsync(string sessionId, string message, string ragContext)
    {
        var history = GetOrCreateHistory(sessionId, ragContext);
        history.AddUserMessage(message);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // Retry with exponential backoff for transient 429 rate limits
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await _chatService.GetChatMessageContentAsync(history, settings, _kernel);
                var responseText = result.Content ?? string.Empty;
                history.AddAssistantMessage(responseText);
                return responseText;
            }
            catch (Exception ex) when (ex.ToString().Contains("429") && attempt < maxRetries)
            {
                var delay = (int)Math.Pow(2, attempt + 1) * 1500;
                _logger.LogWarning("Rate limited (429). Retrying in {Delay}ms (attempt {Attempt}/{Max})...", delay, attempt + 1, maxRetries);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message in session {SessionId}", sessionId);
                return "I'm sorry, I encountered an error processing your request. Please try again in a moment.";
            }
        }

        return "I'm sorry, the AI service is temporarily busy. Please try again in a few seconds.";
    }

    /// <summary>
    /// Sends a system-level instruction to the agent within an existing session.
    /// </summary>
    public async Task<string> SendSystemInstructionAsync(string sessionId, string instruction, string ragContext)
    {
        var history = GetOrCreateHistory(sessionId, ragContext);
        history.AddUserMessage($"[SYSTEM INSTRUCTION]: {instruction}");

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        try
        {
            var result = await _chatService.GetChatMessageContentAsync(history, settings, _kernel);
            var responseText = result.Content ?? string.Empty;
            history.AddAssistantMessage(responseText);
            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing system instruction in session {SessionId}", sessionId);
            return "I'm sorry, I encountered an error. Please try again.";
        }
    }

    private ChatHistory GetOrCreateHistory(string sessionId, string ragContext)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
        {
            var systemPrompt = SystemPromptTemplate.Replace("{CONTEXT}", ragContext);
            history = new ChatHistory(systemPrompt);
            _sessions[sessionId] = history;
        }
        return history;
    }
}

#pragma warning restore SKEXP0070
