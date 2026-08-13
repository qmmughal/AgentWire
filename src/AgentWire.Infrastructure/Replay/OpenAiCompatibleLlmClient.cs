using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AgentWire.Application.Replay;

namespace AgentWire.Infrastructure.Replay;

/// <summary>
/// Talks to any OpenAI-chat-completions-compatible endpoint: api.openai.com, or a
/// self-hosted OpenAI-compatible server such as Ollama's /v1 surface. Authorization
/// header is only sent when an API key is configured, so it works unauthenticated
/// against local servers.
/// </summary>
public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }
        messages.Add(new { role = "user", content = request.UserPrompt });

        var body = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
        };
        if (request.Temperature.HasValue)
        {
            body["temperature"] = request.Temperature.Value;
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{request.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(body)
        };

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmProviderException($"Request to {request.BaseUrl} timed out after {request.TimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new LlmProviderException($"Request to {request.BaseUrl} failed: {ex.Message}", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmProviderException($"Provider returned {(int)response.StatusCode}: {responseBody}");
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            int promptTokens = 0;
            int completionTokens = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt))
                {
                    promptTokens = pt.GetInt32();
                }
                if (usage.TryGetProperty("completion_tokens", out var ct2))
                {
                    completionTokens = ct2.GetInt32();
                }
            }

            return new LlmCompletionResult(content, promptTokens, completionTokens);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new LlmProviderException($"Provider response could not be parsed as an OpenAI-compatible completion: {ex.Message}", ex);
        }
    }
}
