using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Application.Auth;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Data;
using AgentWire.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentWire.Tests.Integration;

/// <summary>
/// Org A is provisioned through the real bootstrap API. Org B is seeded directly via
/// the DbContext + the same ApiKeyHasher/IJwtIssuer the app itself uses, because this
/// build's public API only ever provisions one organization per instance (POST
/// /v1/setup rejects a second) - a real, documented scope boundary, not a gap in this
/// test. It proves the org-scoping mechanism (.ForCurrentOrg()) actually isolates data
/// whenever two organizations exist in the store.
/// </summary>
public sealed class OrgIsolationFixture : AgentWireTestFactory, IAsyncLifetime
{
    public string OrgAApiKey { get; private set; } = null!;
    public string OrgAJwt { get; private set; } = null!;
    public string OrgBJwt { get; private set; } = null!;
    public Guid OrgAPacketId { get; private set; }

    public async Task InitializeAsync()
    {
        var client = CreateClient();
        var bootstrap = await client.BootstrapAsync(orgName: "Org A", email: "org-a-admin@example.com");
        OrgAApiKey = bootstrap.ApiKey;
        OrgAJwt = bootstrap.Jwt;

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
            var jwtIssuer = scope.ServiceProvider.GetRequiredService<IJwtIssuer>();

            var orgB = new Organization { Name = "Org B", Slug = "org-b-" + Guid.NewGuid().ToString("N")[..6] };
            var orgBAdmin = new AppUser
            {
                OrganizationId = orgB.Id,
                Email = "org-b-admin@example.com",
                DisplayName = "Org B Admin",
                Role = UserRole.Admin,
                AuthProvider = AuthProviderType.Local
            };
            db.Organizations.Add(orgB);
            db.AppUsers.Add(orgBAdmin);
            await db.SaveChangesAsync();

            OrgBJwt = jwtIssuer.IssueToken(orgBAdmin);
        }

        var traceResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/v1/traces")
        {
            Headers = { { "X-API-Key", OrgAApiKey } },
            Content = JsonContent.Create(new
            {
                traceId = "org-a-trace",
                agentId = "agent",
                modelProvider = "openai",
                modelName = "gpt-4o-mini",
                systemPrompt = "sys",
                userPrompt = "hello from org A",
                llmResponse = "hi",
                promptTokens = 1,
                completionTokens = 1,
                latencyMs = 5
            })
        });
        var created = await traceResponse.ReadJsonAsync();
        OrgAPacketId = created.GetProperty("id").GetGuid();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}
