# AgentWire Architecture

## What's Real Today

A single ASP.NET Core 10 API (`src/AgentWire.Presentation`, plus `AgentWire.Core`/`Application`/`Infrastructure`), backed by SQLite via EF Core (real migrations, `db.Database.Migrate()` at startup — not `EnsureCreated()`), verified by 57 automated tests in `tests/AgentWire.Tests`:

- **Ingestion**: `POST /v1/traces`, authenticated via a per-organization API key (`X-API-Key`).
- **RBAC**: JWT-claims-based Admin/Member roles, enforced via `[Authorize(Roles=...)]` on every admin route.
- **Multi-tenancy**: every packet/finding/audit row carries an `OrganizationId`, scoped explicitly per query via `.ForCurrentOrg()` (a deliberate trade-off over a global EF query filter — see `AgentWire.Application/Extensions/OrgScopeExtensions.cs`). One organization per self-hosted instance today; there's no second-org provisioning endpoint.
- **SSO**: real OIDC (standard ASP.NET Core OpenID Connect middleware) and real SAML 2.0 SP (`ITfoxtec.Identity.Saml2`, self-signed SP certificate auto-generated on first run). Both funnel into the same local-user find-or-provision-then-issue-JWT pipeline; SSO users are always provisioned as Member, never auto-Admin. One upstream IdP per instance, not per-organization. SAML is validated by a test that hand-signs an assertion against a locally-generated fake IdP — this proves the SP-side XML-dsig validation and provisioning pipeline, not interop with a specific real-world IdP.
- **Security Scanner**: rule-based (regex + Luhn-checked credit cards), runs synchronously on every ingested and replayed packet, findings stored with masked (not raw) matched text.
- **Replay Engine**: calls any OpenAI-chat-completions-compatible endpoint, returns `422` (not a fake success) when unconfigured, `502` on provider failure, re-scans the replayed response through the same security scanner.
- **Audit Log**: append-only (`AgentWire.Core.Entities.AuditLogEntry`) — immutability is enforced by there being no PUT/PATCH/DELETE route under `/v1/audit-log` at all, not a soft-delete flag or a signed ledger.

## Aspirational / Not Yet Built

The distributed, polyglot architecture below is the intended scale-out direction once ingestion volume justifies it — none of it is wired to any code path today. `deploy/docker-compose.yml` starts these services, but the API never talks to them.

### Core Components
- **API Gateway**: Entry point for all incoming traffic (Agents, SDKs, OpenTelemetry). Built on YARP (Yet Another Reverse Proxy) for high performance.
- **Traffic Collector**: High-throughput stateless ingestion service. Validates incoming packets and pushes them to a message broker.
- **Message Broker**: RabbitMQ or MassTransit acts as a buffer to absorb traffic spikes.
- **Traffic Analyzer**: Consumes packets from the queue, enriches them, calculates latencies, and inserts them into storage in batches.
- **Storage Layer**:
  - **PostgreSQL**: Stores relational metadata (Organizations, Projects, Agents, Sessions).
  - **ClickHouse**: Stores the massive volume of AI Packets for fast analytical querying.
  - **Redis**: Caching layer for configurations, rules, and rate limits.

### Database Design

A polyglot persistence model — replacing the current single SQLite database.

#### PostgreSQL Schema
- `Organizations`: Top-level tenant.
- `Projects`: Groups agents and API keys.
- `Agents`: Represents a specific AI agent or service.
- `Sessions`: A logical grouping of traces.

#### ClickHouse Schema
- `AIPackets`: A wide table optimized for time-series and aggregations.
  - `TraceId`, `PacketId`, `ModelName`, `PromptTokens`, `CompletionTokens`, `Cost`, `Latency`, `Timestamp`.

### Scalability Strategy

- **Stateless Services**: Gateway, Collector, and Analyzer are stateless and can scale horizontally using Kubernetes HPA or KEDA.
- **Asynchronous Processing**: Ingestion decoupled from processing via a message broker, with packets processed asynchronously in batches.
- **Batch Inserts**: ClickHouse optimized for batch inserts, targeting tens of thousands of inserts per second.

### Governance hardening beyond what's built

- **Row-Level Security**: today's org-scoping is application-level (`.ForCurrentOrg()` on SQLite), not database-enforced RLS — Postgres RLS is a real future hardening step once/if Postgres is adopted, not what's running now.
- **Signed audit ledger**: today's audit log is an application-level append-only table (no mutation routes) — cryptographic hash-chaining/signing of entries is a possible v2 hardening, not implemented.
- **Custom Security Rules Engine**: today's scanner rules are hardcoded in `PacketScanner.cs`, not user-configurable.
- **Data Masking policies**: today's masking is fixed per finding type (e.g. last-4-digits) inside the scanner, not a customizable policy engine.

## Plugin & SDK Architecture (aspirational)

- **SDKs**: Planned for .NET, Python, and JavaScript, wrapping standard OpenTelemetry SDKs. Not built — today's ingestion is a plain `POST /v1/traces` with an `X-API-Key` header.
- **Plugins**: A pluggable system for LLM providers and MCP servers, with custom cost calculations or security rules. Not built — the replay engine's `ILlmClient` abstraction (`AgentWire.Infrastructure/Replay/OpenAiCompatibleLlmClient.cs`) is the closest real analog today, and it isn't a plugin system.
