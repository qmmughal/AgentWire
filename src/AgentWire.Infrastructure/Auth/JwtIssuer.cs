using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using AgentWire.Application.Auth;
using AgentWire.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AgentWire.Infrastructure.Auth;

public sealed class JwtIssuer : IJwtIssuer
{
    public const string RoleClaimType = "role";
    public const string OrgClaimType = "org_id";
    public const string AuthProviderClaimType = "auth_provider";

    private readonly IConfiguration _config;
    private readonly ILogger<JwtIssuer> _logger;
    private readonly Lazy<byte[]> _signingKeyBytes;

    public JwtIssuer(IConfiguration config, ILogger<JwtIssuer> logger)
    {
        _config = config;
        _logger = logger;
        _signingKeyBytes = new Lazy<byte[]>(ResolveSigningKey);
    }

    public static string Issuer(IConfiguration config) => config["Jwt:Issuer"] ?? "agentwire";
    public static string Audience(IConfiguration config) => config["Jwt:Audience"] ?? "agentwire-api";

    public static SymmetricSecurityKey ResolveSecurityKey(IConfiguration config, ILogger logger)
        => new(ResolveSigningKeyStatic(config, logger));

    private byte[] ResolveSigningKey() => ResolveSigningKeyStatic(_config, _logger);

    private static byte[] ResolveSigningKeyStatic(IConfiguration config, ILogger logger)
    {
        var configuredKey = config["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            return Encoding.UTF8.GetBytes(configuredKey);
        }

        var keyFilePath = config["Jwt:SigningKeyFilePath"]
            ?? Path.Combine(Environment.CurrentDirectory, "jwt-signing-key.txt");

        if (File.Exists(keyFilePath))
        {
            logger.LogInformation("Loaded existing JWT signing key from {Path}", keyFilePath);
            return Convert.FromBase64String(File.ReadAllText(keyFilePath).Trim());
        }

        var newKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(keyFilePath, Convert.ToBase64String(newKey));
        logger.LogWarning(
            "Generated a new JWT signing key at {Path}. All existing tokens are now invalid. " +
            "Set Jwt:SigningKey via configuration/env var for anything beyond local/single-instance use, " +
            "or make sure this file is on a persisted volume.", keyFilePath);
        return newKey;
    }

    public string IssueToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(OrgClaimType, user.OrganizationId.ToString()),
            new(RoleClaimType, user.Role.ToString()),
            new(AuthProviderClaimType, user.AuthProvider.ToString()),
        };

        var key = new SymmetricSecurityKey(_signingKeyBytes.Value);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = _config.GetValue<int?>("Jwt:ExpiryMinutes") ?? 60 * 12;

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer(_config),
            audience: Audience(_config),
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
