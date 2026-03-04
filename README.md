# 🛡️ Aegis — AI Issue Management System

Aegis is an intelligent, conversational issue management platform powered by AI. It acts as a first line of defense — troubleshooting problems through natural conversation, retrieving solutions from a knowledge base, and only escalating to human support when necessary.

## ✨ Key Features

- **Conversational AI Agent** — Natural chatbot interface for reporting and resolving issues
- **RAG-Powered Knowledge Base** — Retrieves relevant solutions from an internal knowledge base using vector similarity search
- **Smart Ticket Escalation** — Only creates support tickets when troubleshooting fails, with user approval via a ticket preview card
- **Human-in-the-Loop (HITL)** — Users review and approve proposed tickets before creation
- **Tool Calling** — Agent can invoke backend tools (e.g., create tickets) autonomously via function calling
- **Provider Agnostic** — Swap LLM providers (Azure OpenAI, Google Gemini, OpenAI, etc.) with a single config change via Microsoft Semantic Kernel

## 🏗️ Architecture

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────┐
│   React UI   │────▶│  Orchestrator    │────▶│  BaseAgent   │
│  (Vite/JSX)  │◀────│  (RAG Injector)  │◀────│  (LLM Brain) │
└──────────────┘     └───────┬──────────┘     └──────┬───────┘
                             │                       │
                     ┌───────▼──────┐        ┌───────▼───────┐
                     │ KnowledgeBase│        │ IT Operations │
                     │  (Vector DB) │        │   Plugin      │
                     └──────────────┘        └───────────────┘
```

- **BaseAgent** — The sole conversational brain. Rich system prompt handles greetings, troubleshooting, follow-up questions, and ticket escalation decisions internally
- **Orchestrator** — Thin layer that fetches RAG context and parses the agent's response for ticket escalation signals
- **KnowledgeBase** — Vector similarity search over IT policies, procedures, and known solutions
- **ITOperationsPlugin** — Tool for creating support tickets, callable by the agent

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React + Vite |
| Backend | ASP.NET Core (.NET 10) |
| AI Framework | Microsoft Semantic Kernel |
| LLM | Azure AI Foundry (gpt-5-mini) / Google Gemini (swappable) |
| Vector Store | In-memory volatile (MVP) |
| Styling | Vanilla CSS with design tokens |

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- An LLM API key (Azure OpenAI or Google Gemini)

### Backend Setup
```bash
cd backend/Aegis.Api

# Set your API key (Azure OpenAI example)
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"

# Run the backend
dotnet run --urls "http://localhost:5000"
```

### Frontend Setup
```bash
cd frontend
npm install
npm run dev -- --port 3000
```

Open [http://localhost:3000](http://localhost:3000) in your browser.

## 🌿 Branch Strategy

| Branch | Description |
|--------|------------|
| `main` | **MVP** — Basic agent with vector context and ticket tool |
| `feature/confidence-rag` | Agent-First architecture with conversational RAG, ticket escalation signals, Azure AI Foundry integration |
| `feature/full-architecture` | Initial full architecture with multi-agent design |

## 📄 License

This project is part of a hackathon submission.
