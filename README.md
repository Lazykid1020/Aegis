# 🛡️ Aegis — AI Issue Management System

> **Branch: `feature/full-architecture`** — Initial Multi-Agent Architecture Exploration

This branch contains the **initial full architecture design** for Aegis, exploring a multi-agent system with specialized agents for different concerns (classification, confidence scoring, risk assessment, etc.).

## 📌 Branch Status: Archived

This branch served as an architectural exploration and has been **superseded** by the Agent-First approach in `feature/confidence-rag`. The multi-agent design proved overly complex for the MVP scope and introduced unnecessary inter-agent communication overhead.

## 🏗️ Architecture Explored

This branch explored a design with multiple specialized agents:
- **Classifier Agent** — Categorizes incoming issues
- **Confidence Scorer** — Evaluates solution confidence
- **Risk Assessor** — Determines risk level of actions
- **Supervisor Agent** — Orchestrates the other agents

### Why We Moved Away

| Concern | Multi-Agent (this branch) | Agent-First (`feature/confidence-rag`) |
|---------|--------------------------|---------------------------------------|
| Complexity | High — multiple agents communicating | Low — single agent with rich prompt |
| Latency | Multiple LLM calls per turn | Single LLM call per turn |
| Maintainability | Many files to update | One system prompt to tune |
| Natural Conversation | Hard to achieve across agents | Native — agent owns the conversation |

## 🔀 Recommended Branches

| Branch | Description |
|--------|------------|
| `main` | ✅ MVP — Start here for the basics |
| `feature/confidence-rag` | ✅ **Recommended** — Agent-First architecture with Azure AI |
| `feature/full-architecture` | 📦 This branch (archived exploration) |

## 📄 Key File

- [`architecture_and_features.md`](./architecture_and_features.md) — Original architecture design document
