# MVP GitHub Issues

## Epic 1: Platform Ingestion & Storage
- **[Core] Implement YARP API Gateway**: Set up the gateway to receive traffic and route to internal services.
- **[Core] Implement Traffic Collector**: Build the stateless service to ingest POST `/v1/traces`, validate schema, and push to RabbitMQ.
- **[Storage] Database Schema (PostgreSQL)**: Define Entity Framework Core models for Organizations, Projects, and Sessions.
- **[Storage] ClickHouse Integration**: Set up ClickHouse client and tables for storing raw `AIPacket` data efficiently.
- **[Storage] RabbitMQ Integration**: Configure MassTransit to handle packet ingestion and processing pipelines.

## Epic 2: Processing & Analytics
- **[Analytics] Token Usage Calculator**: Implement logic in Traffic Analyzer to aggregate token usage per session/project.
- **[Analytics] Cost Engine**: Build a provider-agnostic cost calculator based on `ModelName` and `ProviderName`.
- **[Security] Basic Prompt Injection Scanner**: Implement rule-based detection for prompt injection on incoming packets.

## Epic 3: Frontend Dashboard
- **[UI] Scaffold Next.js Application**: Set up Next.js 14 app router, TailwindCSS, and basic layouts.
- **[UI] Live Traffic View**: Build the NOC-style real-time dashboard using SignalR.
- **[UI] Packet Inspector**: Create a detailed view to show prompt, response, tokens, cost, and latency for a given packet.
- **[UI] Project Overview**: Implement charts using Apache ECharts for cost and token usage over time.

## Epic 4: SDKs & Tooling
- **[SDK] OpenTelemetry .NET Exporter**: Create a custom exporter or configure OTLP to send traces to AgentWire Gateway.
- **[Infra] Docker Compose Local Stack**: Ensure the entire stack (Gateway, Collector, Postgres, ClickHouse, Redis, RabbitMQ) runs seamlessly locally.
