# AgentWire

AgentWire is the **OpenTelemetry + Wireshark + Cloudflare for AI Agents**.

Its purpose is to monitor, inspect, analyze, secure, replay, and optimize all traffic flowing between Users, AI Agents, LLMs, MCP Servers, and external services.

## Features

- **Live Traffic Monitor**: Real-time dashboard similar to a network operations center.
- **AI Packet Inspector**: Inspect every prompt, context, memory, tool request, and model output.
- **Replay Engine**: Replay any execution with different prompts, models, or temperatures.
- **Prompt Version Control**: Track history, diffs, and success rates of all prompts.
- **Security Scanner**: Detect prompt injection, sensitive data leakage, and malicious MCP servers.
- **Cost Intelligence**: Detailed cost analytics broken down by organization, project, model, and user.
- **Search Engine**: Advanced search to find specific executions, errors, or security events.
- **Plugin System**: Support for OpenAI, Anthropic, Gemini, MCP Servers, and more.

## Architecture

AgentWire is built on a scalable, cloud-native architecture using:
- **Backend**: ASP.NET Core 10, .NET Aspire
- **Database**: PostgreSQL (Metadata) + ClickHouse (High-volume packet storage) + Redis (Caching)
- **Frontend**: Next.js, React, Tailwind CSS, React Flow, Apache ECharts
- **Infrastructure**: Docker, Kubernetes, Helm, Terraform

See [docs/architecture.md](docs/architecture.md) for more details.

## Getting Started

*(Documentation coming soon)*

## License

This project is licensed under the Apache 2.0 License - see the [LICENSE](LICENSE) file for details.
