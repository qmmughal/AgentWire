using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentWire.Tests.Fixtures;

/// <summary>
/// Each test class gets its own temp-file SQLite database and its own JWT/SAML key
/// material, isolated in a per-instance temp directory that's deleted on dispose.
/// Using a real file-backed SQLite provider (not InMemory) so unique-index/FK
/// enforcement - which several tests rely on - actually happens.
/// </summary>
public class AgentWireTestFactory : WebApplicationFactory<Program>
{
    private readonly string _tempDir;
    private readonly SemaphoreSlim _bootstrapLock = new(1, 1);
    private HttpTestExtensions.BootstrapResult? _bootstrapResult;

    public AgentWireTestFactory()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agentwire-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Idempotent bootstrap for test classes that share ONE factory instance across
    /// several test methods via IClassFixture: xUnit's IAsyncLifetime.InitializeAsync
    /// runs once per test METHOD (a fresh test-class instance per method), not once
    /// per fixture - calling POST /v1/setup directly from there would 409 on every
    /// method after the first. This caches the first successful bootstrap and hands
    /// the same credentials back to every subsequent caller instead.
    /// </summary>
    public async Task<HttpTestExtensions.BootstrapResult> EnsureBootstrappedAsync(
        string orgName = "Test Org",
        string email = "admin@test.local",
        string password = "supersecret123")
    {
        await _bootstrapLock.WaitAsync();
        try
        {
            _bootstrapResult ??= await CreateClient().BootstrapAsync(orgName, email, password);
            return _bootstrapResult;
        }
        finally
        {
            _bootstrapLock.Release();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(_tempDir, "agentwire-test.db")}",
                ["Jwt:SigningKeyFilePath"] = Path.Combine(_tempDir, "jwt-signing-key.txt"),
                ["Jwt:Issuer"] = "agentwire-test",
                ["Jwt:Audience"] = "agentwire-api-test",
                ["Saml:GeneratedCertificatePath"] = Path.Combine(_tempDir, "saml-sp-cert.pfx"),
                ["Saml:PublicBaseUrl"] = "http://localhost",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // best-effort cleanup only
            }
        }
    }
}
