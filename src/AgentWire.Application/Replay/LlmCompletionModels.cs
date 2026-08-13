namespace AgentWire.Application.Replay;

public sealed record LlmCompletionRequest(
    string BaseUrl,
    string? ApiKey,
    string Model,
    string? SystemPrompt,
    string UserPrompt,
    double? Temperature,
    int TimeoutSeconds);

public sealed record LlmCompletionResult(
    string ResponseText,
    int PromptTokens,
    int CompletionTokens);
