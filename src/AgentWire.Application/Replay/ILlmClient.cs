using System.Threading;
using System.Threading.Tasks;

namespace AgentWire.Application.Replay;

/// <summary>
/// A provider-agnostic client for OpenAI-chat-completions-compatible endpoints
/// (works against api.openai.com or any self-hosted OpenAI-compatible server, e.g. Ollama).
/// </summary>
public interface ILlmClient
{
    Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct);
}

public sealed class LlmNotConfiguredException : System.Exception
{
    public LlmNotConfiguredException(string message) : base(message)
    {
    }
}

public sealed class LlmProviderException : System.Exception
{
    public LlmProviderException(string message, System.Exception? inner = null) : base(message, inner)
    {
    }
}
