# AgentWire Roadmap

## v1.0 (MVP) - Foundation & Observability
- [x] Core ingestion API (`POST /v1/traces`, ApiKey-authenticated, org-scoped)
- [ ] PostgreSQL + ClickHouse storage implementation (still SQLite — see [docs/architecture.md](architecture.md))
- [ ] Next.js Real-time Dashboard (NOC view) (dashboard is still an unmodified `create-next-app` scaffold)
- [x] Basic AI Packet Inspector (`GET /v1/packets`, `GET /v1/packets/{id}`)
- [x] Token Usage and Cost Analytics (`GET /v1/analytics/costs`)
- [ ] .NET and Python SDKs
- [ ] Docker Compose for easy self-hosting (compose file starts Postgres/Redis/RabbitMQ/ClickHouse, but they aren't wired to the app yet)

## v2.0 - Security & Interactivity
- [x] Real-time Security Scanner (Prompt Injection, PII leakage) — rule-based, runs inline on ingestion and replay
- [x] Replay Engine (`POST /v1/packets/{id}/replay` against any OpenAI-compatible provider — API only, no UI yet)
- [ ] Prompt Version Control & Diff Viewer
- [ ] Alert Engine (Webhooks, Slack, Email integrations)
- [ ] Interactive Execution Graph (React Flow)
- [ ] Advanced Search Engine (findings list supports basic type/severity filters only)
- [ ] Kubernetes Helm Charts

## v3.0 - Enterprise Governance & Scale (100% Open Source)
- [x] Multi-tenant isolation architecture (`OrganizationId`-scoped; single org per self-hosted instance today, no second-org provisioning endpoint yet)
- [x] Role-Based Access Control (RBAC) & SSO (SAML/OIDC) — both real; SAML validated against a hand-signed test assertion, not a live third-party IdP
- [ ] Custom Security Rules Engine (today's rules are hardcoded in `PacketScanner.cs`, not user-configurable)
- [ ] Data Retention Policies and Cold Storage Tiering (S3)
- [ ] AI-powered Failure Analysis and Optimization Suggestions
- [ ] Advanced Cost Intelligence (Predictive analytics)
- [x] Immutable Audit Logs (`GET /v1/audit-log`, Admin-only — no mutation route exists)
