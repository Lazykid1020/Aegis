using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0070

namespace Aegis.Api.Services;

/// <summary>
/// The sole conversational brain of Aegis. Handles all user interaction,
/// troubleshooting, clarifying questions, and ticket escalation decisions
/// via its system prompt — no external confidence evaluator needed.
/// </summary>
public class BaseAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<BaseAgent> _logger;
    private static readonly Dictionary<string, ChatHistory> _sessions = new();

    private const string SystemPromptTemplate = @"You are Aegis, a friendly and highly capable enterprise IT support chatbot.

## YOUR PERSONALITY
- You are warm, professional, and conversational — never robotic.
- You greet users naturally and ask how you can help.
- You ask follow-up questions to understand problems better before jumping to solutions.

## YOUR CAPABILITIES
- You have access to Knowledge Base articles (provided below) that contain solutions to common IT issues.
- You have a tool called `create_support_ticket` that creates an IT support ticket when human intervention is truly needed.

## HOW YOU WORK — IMPORTANT RULES

1. **BE CONVERSATIONAL FIRST**: When a user describes a problem, ask clarifying questions if needed. Understand the full picture before offering solutions.

2. **TROUBLESHOOT USING YOUR KNOWLEDGE**: Use the Knowledge Base Context provided to help users solve their issues. Offer step-by-step solutions. Be specific and helpful.

3. **ITERATE ON SOLUTIONS**: If a solution does not work for the user, acknowledge it, and try a different approach if you have one. Do NOT give up after one attempt.

4. **NEVER EXPOSE INTERNAL METRICS**: Do NOT mention 'confidence scores', 'knowledge base articles', 'RAG', 'context retrieval', or any internal system details to the user. You are just a helpful IT assistant.

5. **TICKET ESCALATION — ONLY AS A LAST RESORT**: You should ONLY suggest creating a support ticket when:
   - You have genuinely exhausted all troubleshooting steps from your knowledge base
   - The issue clearly requires physical intervention (broken hardware, etc.)
   - The user explicitly asks you to create a ticket
   
   When you decide a ticket is needed, you MUST output the following JSON block on its own line, wrapped in a special marker. This is how the system detects your escalation intent:
   
   :::TICKET_SIGNAL:::
   {""action"": ""REQUEST_TICKET_APPROVAL"", ""title"": ""<short title>"", ""description"": ""<summary of the issue and what was tried>"", ""category"": ""<IT|HR|Facilities|Security>"", ""urgency"": ""<Low|Medium|High|Critical>""}
   :::END_SIGNAL:::
   
   Before this JSON block, write a natural message to the user explaining that you think this needs human attention and showing them what the ticket would look like. NEVER call the `create_support_ticket` tool directly — always output the signal above and wait for the user to approve.

6. **AFTER APPROVAL**: If the system tells you the user approved the ticket, THEN use the `create_support_ticket` tool with the details you proposed.

7. **AFTER REJECTION**: If the user rejects the ticket, acknowledge it and ask if there's anything else you can help with.

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

        try
        {
            var result = await _chatService.GetChatMessageContentAsync(history, settings, _kernel);
            var responseText = result.Content ?? string.Empty;
            history.AddAssistantMessage(responseText);
            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in session {SessionId}", sessionId);
            return "I'm sorry, I encountered an error processing your request. Please try again.";
        }
    }

    /// <summary>
    /// Sends a system-level instruction to the agent within an existing session
    /// (e.g., "The user approved the ticket. Create it now.").
    /// </summary>
    public async Task<string> SendSystemInstructionAsync(string sessionId, string instruction, string ragContext)
    {
        var history = GetOrCreateHistory(sessionId, ragContext);
        // We inject the instruction as a user message prefixed with a system tag
        // so the LLM treats it as an authoritative directive
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
