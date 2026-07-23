<div align="center">
  <h1>AgentWire</h1>
  <p><strong>Observability, security, and cost analytics for AI agents, LLMs, and MCP servers</strong></p>
  <p><em>OpenTelemetry-inspired traffic inspection for the agentic stack — status: <strong>Alpha / MVP</strong></em></p>

  <p>
    <a href="https://github.com/qmmughal/AgentWire/stargazers"><img src="https://img.shields.io/github/stars/qmmughal/AgentWire?style=for-the-badge" alt="Stars Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/network/members"><img src="https://img.shields.io/github/forks/qmmughal/AgentWire?style=for-the-badge" alt="Forks Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/issues"><img src="https://img.shields.io/github/issues/qmmughal/AgentWire?style=for-the-badge" alt="Issues Badge"/></a>
    <a href="https://github.com/qmmughal/AgentWire/blob/main/LICENSE"><img src="https://img.shields.io/github/license/qmmughal/AgentWire?style=for-the-badge" alt="License Badge"/></a>
  </p>
</div>

---

## Status

AgentWire is in **active MVP development**. The runnable path today is a local ingestion API + packet/cost endpoints (SQLite). The full gateway / ClickHouse / security / replay stack described in the architecture docs is **planned** — see [docs/roadmap.md](docs/roadmap.md).

| Available now (MVP) | Planned (roadmap) |
|---|---|
| Trace ingest `POST /v1/traces` | YARP gateway + collector microservices |
| Packet list `GET /v1/packets` | ClickHouse analytics store |
| Cost rollup `GET /v1/analytics/costs` | Prompt injection scanner, replay engine |
| Optional Next.js dashboard scaffold | Multi-tenant SaaS / enterprise edition |

---

## Monetization plan

The MVP above stays free and open source — that's how a project earns the kind of trust [ckeditor5-blazor](https://github.com/qmmughal/ckeditor5-blazor) built organically over several years. The plan is to keep it that way for the core ingest/cost API, and eventually offer a **hosted, managed tier** covering the parts of the roadmap that are genuinely expensive to self-host well: the ClickHouse analytics store, the security scanner, and multi-tenant SaaS operations (see the "Enterprise Edition" section of [docs/roadmap.md](docs/roadmap.md)).

No hosted beta or waitlist exists yet — this section is a heads-up, not a pitch. It'll get a real signup link once there's something to sign up for.

## About

AgentWire sits between users, agents, and LLM/MCP providers so you can **monitor, inspect, attribute cost, and (eventually) secure and replay** AI traffic — similar in spirit to what Wireshark and edge gateways do for networks.

---

## Getting Started (MVP — local)

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
  -d "{
    \"traceId\": \"demo-001\",
    \"agentId\": \"support-bot\",
    \"modelProvider\": \"openai\",
    \"modelName\": \"gpt-4o-mini\",
    \"systemPrompt\": \"You are a helpful assistant.\",
    \"userPrompt\": \"Hello\",
    \"llmResponse\": \"Hi there!\",
    \"promptTokens\": 12,
    \"completionTokens\": 8,
    \"latencyMs\": 220
  }"
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

### 5. Optional — local infrastructure only

Postgres, Redis, RabbitMQ, and ClickHouse can be started for upcoming services:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

App container builds are not wired yet (Dockerfiles pending). Use `dotnet run` for the MVP API.

---

## Architecture

Target architecture (event-driven gateway + collector + analytics):

- **Backend**: ASP.NET Core 10
- **Storage (planned)**: PostgreSQL (metadata) + ClickHouse (packets) + Redis
- **Frontend**: Next.js dashboard scaffold
- **Infra sketches**: Docker Compose, Kubernetes, Terraform under `deploy/`

Details: [docs/architecture.md](docs/architecture.md) · Roadmap: [docs/roadmap.md](docs/roadmap.md)

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Issues that match MVP epics are sketched in [docs/issues-mvp.md](docs/issues-mvp.md).

---

## License

Apache 2.0 — see [LICENSE](LICENSE).
