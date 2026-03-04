# 🛡️ Aegis — AI Issue Management System

> **Branch: `feature/confidence-rag`** — Agent-First Architecture with Conversational RAG

This branch implements the **Agent-First** architecture, a major refactor from the MVP on `main`. The AI agent is now the sole conversational brain — it handles greetings, troubleshooting, natural information gathering, and ticket escalation decisions entirely through its system prompt, with no external micromanagement.

## 🆕 What's New in This Branch

### Agent-First Architecture
- **BaseAgent as the Sole Brain** — All conversational logic, troubleshooting, and escalation decisions live in the agent's rich system prompt
- **Orchestrator Simplified** — Reduced to a thin RAG context injector (~140 lines). No more separate confidence LLM calls or turn-by-turn micromanagement
- **Internal Confidence Management** — The agent manages confidence internally via its prompt. No scores are exposed to users

### Ticket Escalation via Structured Signals
- Agent emits a `:::TICKET_SIGNAL:::` JSON block when it decides a ticket is needed
- Orchestrator parses the signal with regex and presents a **Ticket Preview Card** to the user
- User approves or rejects before the ticket is created (Human-in-the-Loop)
- Buttons disappear after the user makes a choice

### Azure AI Foundry Integration
- Switched from Google Gemini to **Azure AI Foundry (gpt-5-mini)** for better rate limits and performance
- Exponential backoff retry logic (3s → 6s → 12s) for transient 429 errors
- One-line provider swap via Microsoft Semantic Kernel connectors

### UI Improvements
- Purple **Ticket Preview Card** with Title, Category, Urgency, and Description fields
- Removed feedback buttons (feedback is now conversational)
- Removed confidence score display from the header
- Approve/Reject buttons auto-hide after user clicks

## 📁 Key Files Changed (vs `main`)

| File | Change |
|------|--------|
| `backend/.../Services/BaseAgent.cs` | Complete rewrite — rich system prompt, retry logic, ticket signal mechanism |
| `backend/.../Services/OrchestratorService.cs` | Simplified to thin RAG injector + signal parser |
| `backend/.../Models/ChatResponse.cs` | Added `TicketPreview` model |
| `backend/.../Controllers/ChatController.cs` | Removed feedback endpoint, kept Chat/Approve/Reject |
| `backend/.../Program.cs` | Azure OpenAI connector replaces Google Gemini |
| `frontend/src/App.jsx` | Ticket preview card, removed feedback buttons, auto-hide approve/reject |
| `frontend/src/api.js` | Removed `submitFeedback` |
| `frontend/src/index.css` | Ticket preview card styles with urgency color coding |

## 🔄 Conversation Flow

```
User Message
    │
    ▼
┌─────────────────┐
│  Orchestrator    │──▶ Fetch RAG Context from KnowledgeBase
│  (Thin Layer)    │
└────────┬────────┘
         │  Pass message + RAG context
         ▼
┌─────────────────┐
│   BaseAgent     │──▶ LLM processes with rich system prompt
│  (GPT-5-mini)   │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
 Normal    Ticket Signal
 Response  (:::TICKET_SIGNAL:::)
    │         │
    ▼         ▼
 Return    Parse JSON → Show Preview Card
 to UI     → User Approves → Create Ticket
```

## 🚀 Running This Branch

```bash
# Backend
cd backend/Aegis.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
dotnet run --urls "http://localhost:5000"

# Frontend
cd frontend
npm install
npm run dev -- --port 3000
```

## 🧪 Test Scenarios

| Scenario | Expected Behavior |
|----------|------------------|
| "hey" / "hello" | Natural greeting, no ticket, no confidence score |
| "How do I connect to guest WiFi?" | RAG answer with network name + password |
| "My laptop screen is cracked" | Clarifying questions → Ticket Preview Card → Approve/Reject |
| Approve ticket | Ticket created via tool, agent offers additional help |
| Reject ticket | Agent acknowledges and asks if there's anything else |

## 🔀 Branch Strategy

| Branch | Status | Description |
|--------|--------|------------|
| `main` | ✅ Stable | MVP — Basic agent with vector context and ticket tool |
| **`feature/confidence-rag`** | ✅ **Active** | Agent-First architecture (this branch) |
| `feature/full-architecture` | 📦 Archive | Initial multi-agent design exploration |
