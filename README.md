<div align="center">
  <h1>🚀 AgentWire</h1>
  <p><strong>Observability, security, and cost analytics for AI agents, LLMs, and MCP servers</strong></p>
  <p><em>OpenTelemetry-inspired traffic inspection for the agentic stack — 100% Free & Open Source</em></p>

  <p>
    <a href="https://github.com/qmmughal/AgentWire/stargazers"><img src="https://img.shields.io/github/stars/qmmughal/AgentWire?style=for-the-badge" alt="Stars Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/network/members"><img src="https://img.shields.io/github/forks/qmmughal/AgentWire?style=for-the-badge" alt="Forks Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/issues"><img src="https://img.shields.io/github/issues/qmmughal/AgentWire?style=for-the-badge" alt="Issues Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/blob/main/LICENSE"><img src="https://img.shields.io/github/license/qmmughal/AgentWire?style=for-the-badge" alt="License Badge"/></a>
  </p>
</div>

---

## 📖 About AgentWire

AgentWire is a **100% free and open-source observability, governance, and security gateway** specifically designed for modern AI Agents, Large Language Models (LLMs), and Model Context Protocol (MCP) servers. Just as Wireshark inspects network packets and Cloudflare provides a protective edge layer, AgentWire sits between your users, agents, and external services to monitor, inspect, analyze, secure, replay, and optimize every single AI interaction.

> [!NOTE]
> **100% Open Source Commitment**: AgentWire is built from the ground up to be 100% free and open source under the Apache 2.0 License. All features—including multi-tenancy, RBAC, SSO/OIDC, security guardrails, audit logging, custom rules, and long-term storage—are included directly in the open-source repository with zero commercial paywalls, open-core restrictions, or proprietary tiers.

---

## ✨ Features

- 🚦 **Live Traffic Monitor**: Real-time dashboard similar to a network operations center.
- 🕵️‍♂️ **AI Packet Inspector**: Deep inspection of every prompt, context, memory, tool request, and model output.
- ⏪ **Replay Engine**: Replay any execution with different prompts, models, or temperatures to debug or optimize.
- 📚 **Prompt Version Control**: Track history, diffs, and success rates of all prompts.
- 🛡️ **Security & Guardrails**: Detect prompt injections, sensitive data leakage (PII/PHI), and malicious MCP server behavior.
- 🏢 **Multi-Tenancy & Governance**: Built-in Organization isolation, Role-Based Access Control (RBAC), SSO (OIDC/SAML), and immutable audit logs.
- 💸 **Cost Intelligence**: Detailed cost analytics broken down by organization, project, model, and user.
- 🔍 **Advanced Search**: Global search to find specific executions, errors, semantic matches, or security events.
- 🔌 **Universal Plugin System**: Seamless integration with OpenAI, Anthropic, Gemini, local models, and MCP Servers.

---

## 🚀 Getting Started (MVP — local)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Optional: Node.js 18+ for the dashboard scaffold under `src/Client/dashboard`

### 1. Clone and run the API

```bash
git clone https://github.com/qmmughal/AgentWire.git
cd AgentWire

dotnet run --project src/AgentWire.Presentation
```

By default the API listens on **http://localhost:5102** (see `launchSettings.json`).

### 2. Ingest a sample trace

```bash
curl -X POST http://localhost:5102/v1/traces \
  -H "Content-Type: application/json" \
  -d '{
    "traceId": "demo-001",
    "agentId": "support-bot",
    "modelProvider": "openai",
    "modelName": "gpt-4o-mini",
    "systemPrompt": "You are a helpful assistant.",
    "userPrompt": "Hello",
    "llmResponse": "Hi there!",
    "promptTokens": 12,
    "completionTokens": 8,
    "latencyMs": 220
  }'
```

### 3. Inspect packets and costs

```bash
curl http://localhost:5102/v1/packets
curl http://localhost:5102/v1/analytics/costs
```

### 4. Optional — dashboard scaffold

```bash
cd src/Client/dashboard
npm install
npm run dev
```

Open http://localhost:3000 (configure `NEXT_PUBLIC_API_URL` to point at the API if needed).

### 5. Optional — local infrastructure stack

Postgres, Redis, RabbitMQ, and ClickHouse can be started for background services:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

---

## 🏗️ Architecture

AgentWire is built on a scalable, event-driven microservices architecture:

- **Backend**: ASP.NET Core 10, .NET Aspire
- **Storage**: PostgreSQL (metadata) + ClickHouse (packets) + Redis
- **Frontend**: Next.js dashboard
- **Infra**: Docker Compose, Kubernetes, Terraform under `deploy/`

Details: [docs/architecture.md](docs/architecture.md) · Roadmap: [docs/roadmap.md](docs/roadmap.md)

---

## 🤝 Contributing

We welcome contributions from the community! See [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/issues-mvp.md](docs/issues-mvp.md).

---

## 🏷️ Tags & Keywords
`AI Agents` `LLM Observability` `AI Security` `Prompt Injection` `MCP Servers` `Model Context Protocol` `AI Gateway` `LLM Analytics` `AI Traffic Monitor` `Generative AI` `Agentic AI` `AI Infrastructure` `Machine Learning` `LLMOps` `AIOps` `OpenTelemetry for AI` `Wireshark for AI` `Cloudflare for AI` `Open Source AI` `FOSS AI`

---

## 📝 License

This project is **100% Free & Open Source** and licensed under the [Apache 2.0 License](LICENSE) - see the [LICENSE](LICENSE) file for details.
