using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentWire.Infrastructure.Auth;

/// <summary>
/// Resolves the SP's own signing/encryption certificate for SAML. If Saml:CertificatePath
/// isn't configured, generates a self-signed cert on first run and persists it next to the
/// SQLite DB - same "lost on restart if not on a persisted volume" caveat as the JWT signing
/// key (JwtIssuer), logged clearly for the same reason.
/// </summary>
public static class SamlCertificateProvider
{
    public static X509Certificate2 GetOrCreateSpCertificate(IConfiguration config, ILogger logger)
    {
        var configuredPath = config["Saml:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configuredPassword = config["Saml:CertificatePassword"] ?? string.Empty;
            logger.LogInformation("Loaded configured SAML SP certificate from {Path}", configuredPath);
            return X509CertificateLoader.LoadPkcs12FromFile(
                configuredPath, configuredPassword, X509KeyStorageFlags.Exportable);
        }

        var pfxPath = config["Saml:GeneratedCertificatePath"]
            ?? Path.Combine(Environment.CurrentDirectory, "saml-sp-cert.pfx");
        var passwordPath = pfxPath + ".pass";

        if (File.Exists(pfxPath) && File.Exists(passwordPath))
        {
            var password = File.ReadAllText(passwordPath).Trim();
            logger.LogInformation("Loaded existing self-signed SAML SP certificate from {Path}", pfxPath);
            return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password, X509KeyStorageFlags.Exportable);
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=AgentWire SP", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

        var newPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var pfxBytes = cert.Export(X509ContentType.Pfx, newPassword);

        File.WriteAllBytes(pfxPath, pfxBytes);
        File.WriteAllText(passwordPath, newPassword);

        logger.LogWarning(
            "Generated a new self-signed SAML SP certificate at {Path}. This certificate " +
            "must be re-registered with your IdP if it is regenerated (e.g. after losing " +
            "an unpersisted volume). Set Saml:CertificatePath for anything beyond " +
            "local/single-instance use.", pfxPath);

        return X509CertificateLoader.LoadPkcs12(pfxBytes, newPassword, X509KeyStorageFlags.Exportable);
    }
}
