using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AgentWire.Tests.Fixtures;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }

    public static StubHttpMessageHandler OpenAiStyleSuccess(string responseText = "Hello from the stub provider.") =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
            {
              "choices": [ { "message": { "content": "{{responseText}}" } } ],
              "usage": { "prompt_tokens": 11, "completion_tokens": 4 }
            }
            """, System.Text.Encoding.UTF8, "application/json")
        });
}
