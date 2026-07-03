# AgentWire Architecture

## High-Level System Architecture

AgentWire is built using a scalable, event-driven, microservices architecture designed to handle extremely high throughput of AI packets.

### Core Components
- **API Gateway**: Entry point for all incoming traffic (Agents, SDKs, OpenTelemetry). Built on YARP (Yet Another Reverse Proxy) for high performance.
- **Traffic Collector**: High-throughput stateless ingestion service. Validates incoming packets and pushes them to a message broker.
- **Message Broker**: RabbitMQ or MassTransit acts as a buffer to absorb traffic spikes.
- **Traffic Analyzer**: Consumes packets from the queue, enriches them, calculates latencies, and inserts them into storage in batches.
- **Security Scanner**: A parallel service that analyzes packet content for prompt injections, secrets, and malicious patterns.
- **Cost Engine**: Calculates the exact cost of each packet based on provider and model pricing.
- **Storage Layer**:
  - **PostgreSQL**: Stores relational metadata (Organizations, Projects, Agents, Sessions).
  - **ClickHouse**: Stores the massive volume of AI Packets for fast analytical querying.
  - **Redis**: Caching layer for configurations, rules, and rate limits.

## Database Design

AgentWire uses a polyglot persistence model.

### PostgreSQL Schema
- `Organizations`: Top-level tenant.
- `Projects`: Groups agents and API keys.
- `Agents`: Represents a specific AI agent or service.
- `Sessions`: A logical grouping of traces.

### ClickHouse Schema
- `AIPackets`: A wide table optimized for time-series and aggregations.
  - `TraceId`, `PacketId`, `ModelName`, `PromptTokens`, `CompletionTokens`, `Cost`, `Latency`, `Timestamp`.

## Scalability Strategy

- **Stateless Services**: Gateway, Collector, and Analyzer are stateless and can scale horizontally using Kubernetes HPA or KEDA.
- **Asynchronous Processing**: Ingestion is decoupled from processing. Clients receive a `202 Accepted` immediately, and packets are processed asynchronously.
- **Batch Inserts**: ClickHouse is optimized for batch inserts. The Traffic Analyzer batches packets before writing to ClickHouse, allowing for tens of thousands of inserts per second.

## Security Architecture

- **Data Masking**: PII and sensitive data can be masked before storage using customizable rules.
- **Prompt Injection Detection**: Built-in rules to detect common injection patterns.
- **Tenant Isolation**: PostgreSQL uses Row-Level Security (RLS) to ensure data isolation between organizations.

## Plugin & SDK Architecture

- **SDKs**: Available for .NET, Python, and JavaScript. They wrap standard OpenTelemetry SDKs, making it easy to integrate AgentWire into existing projects.
- **Plugins**: AgentWire supports plugins for different LLM providers (OpenAI, Anthropic) and MCP servers. Plugins can provide custom cost calculations or security rules.
