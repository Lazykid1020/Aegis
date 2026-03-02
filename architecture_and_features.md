# Aegis (Ryan Resolve AI) - Hackathon Architecture & Concept

## The Challenge
Design an end-to-end, scalable, governance-aligned, intelligent, and intuitive AI-enabled Issues Management solution. The lifecycle must cover: intake -> analysis -> remediation -> sustainment -> closure.

## Core Features
1. **Dual-Agent Architecture**: A `Supervisor Agent` to understand intent/gather info, and a `Guardrail Agent` to assess risk and company policy.
2. **Dynamic Confidence Scoring (Experience-Based)**: Before acting, the system calculates a Confidence Score (0-100%). This is not just naive similarity. It combines:
   - *Similarity Score*: How closely the request matches past tickets in the Vector DB.
   - *Historical Success Rate (Experience)*: The DB returns the *status* of similar past tickets. If prior similar tickets were successfully resolved by an AI tool (e.g., `BrowserAgent`), confidence goes up. If they were escalated to humans, confidence goes down.
   - *LLM Certainty*: The LLM evaluates its own ability to parse the exact parameters needed for the tools.
3. **Risk & Confidence Tiering**:
   - *Tier 1 (High Confidence, Low Risk)*: Auto-remediation (e.g., password reset).
   - *Tier 2 (High Confidence, Medium Risk)*: Human-in-the-Loop (HITL). Prepares action, pauses for approval.
   - *Tier 3 (Low Confidence, High Risk)*: Escalation. Raises a rich, contextual ticket for a human.
4. **UI/Browser Automation Agent**: For legacy systems without APIs, the AI visually navigates the web portal to solve issues.
5. **Vector DB as an Experience Engine**: Unlike naive RAG, the agent queries the DB to learn *how* to act, understand environmental context/blast radius, and enforce strict governance policies.
6. **Continuous Learning Loop**: When human agents resolve escalated tickets, resolution notes are embedded into the DB, making the system smarter over time.
7. **Plug-and-Play Microservice**: API-first layer that can connect to any interface (Slack, Teams, Web UI).

## Proposed Technical Stack
- **Agent Framework**: Microsoft Semantic Kernel (.NET)
- **LLM**: GPT-4o / Claude 3.5 Sonnet (via Azure OpenAI or direct API)
- **Vector DB**: Supabase (pgvector) / Azure AI Search
- **Backend Orchestration**: C# + .NET 8 Web API
- **Frontend UI**: Next.js (Vercel AI SDK) or Blazor (for a pure .NET stack)
- **Browser Automation**: Playwright (.NET bindings)

## Architecture Diagram
```mermaid
graph TD
    classDef user fill:#3b82f6,stroke:#1d4ed8,stroke-width:2px,color:#fff
    classDef orchestration fill:#8b5cf6,stroke:#5b21b6,stroke-width:2px,color:#fff
    classDef guardrail fill:#ef4444,stroke:#b91c1c,stroke-width:2px,color:#fff
    classDef knowledge fill:#10b981,stroke:#047857,stroke-width:2px,color:#fff
    classDef action fill:#f59e0b,stroke:#b45309,stroke-width:2px,color:#fff
    classDef feedback fill:#ec4899,stroke:#be185d,stroke-width:2px,color:#fff

    U["User Interface (Chat / Slack / Web)"]:::user 
    
    U -->|"1. Natural Language Request"| O

    subgraph Core_AI_Engine ["Core AI Engine (The Brain)"]
        direction TB
        O["Supervisor Agent (Intent Gathering)"]:::orchestration
        C["Confidence and Risk Scorer"]:::orchestration
        G["Guardrail Agent (Policy Assessor)"]:::guardrail
        
        O -->|"2. Analyzes Intent"| C
        C <-->|"3. Checks Policy"| G
    end

    subgraph Knowledge_Base ["Knowledge Base (Vector DB)"]
        V["Vector Database (Experience Engine and SOPs)"]:::knowledge
    end
    
    C <-->|"Calculates Similarity Score"| V
    G <-->|"Checks Security Policies"| V
    
    C -->|"High Confidence (>80%) & Low Risk"| T1["Tier 1: Auto-Remediation / Solution"]:::action
    C -->|"High Confidence & Medium Risk"| T2["Tier 2: Human-in-the-Loop Approval"]:::action
    C -->|"Low Confidence (<80%) OR High Risk"| T3["Tier 3: Gather Info & Raise Ticket"]:::action

    subgraph Execution_Layer ["Remediation & Execution"]
        A1["API Tool Registry"]:::action
        A2["Browser UI Agent"]:::action
        A3["Ticketing System (ServiceNow/Jira)"]:::action
    end

    T1 -->|"Executes API"| A1
    T1 -->|"Navigates UI"| A2
    T2 -->|"Requests UI Approval"| U
    T3 -->|"Auto-Generates Ticket"| A3
    
    T1 -.->|"Provides Solution"| U
    
    subgraph Maintenance_Loop ["Database Maintenance & Continuous Learning"]
        F["User Feedback / Resolution Status"]:::feedback
        H["Human Agent Works Ticket"]:::feedback
        I["Auto-Embedder (Knowledge Updates)"]:::knowledge
    end

    U -->|"Negative Feedback"| F
    U -->|"Positive Feedback"| F
    
    F -->|"Failed to Resolve"| A3
    F -->|"Resolved Successfully"| I
    
    A3 -->|"Assigned to"| H
    H -->|"Closes Ticket with Notes"| I
    
    I -->|"Embeds & Ingests New Data"| V
```
