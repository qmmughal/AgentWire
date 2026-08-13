using System;
using System.Linq;
using AgentWire.Infrastructure.Auth;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentWire.Presentation.Auth;

/// <summary>
/// Builds the SP's Saml2Configuration for a self-hosted, single-upstream-IdP deployment
/// (one AgentWire instance trusts one IdP - not a multi-tenant "bring your own IdP per
/// organization" system). The IdP is onboarded via its standard metadata (URL or a local
/// file), which is how virtually every real IdP (Okta, Entra ID, Keycloak) is configured.
/// </summary>
public static class Saml2ConfigurationFactory
{
    public static Saml2Configuration Build(IConfiguration config, ILogger logger)
    {
        var publicBaseUrl = (config["Saml:PublicBaseUrl"] ?? "http://localhost:5102").TrimEnd('/');
        var spCert = SamlCertificateProvider.GetOrCreateSpCertificate(config, logger);

        var saml2Config = new Saml2Configuration
        {
            Issuer = config["Saml:EntityId"] ?? $"{publicBaseUrl}/saml/metadata",
            SignatureAlgorithm = Saml2SecurityAlgorithms.RsaSha256Signature,
            SigningCertificate = spCert,
            CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None,
            AudienceRestricted = true,
        };
        saml2Config.AllowedAudienceUris.Add(saml2Config.Issuer);

        var metadataUrl = config["Saml:IdpMetadataUrl"];
        var metadataPath = config["Saml:IdpMetadataXmlPath"];

        if (string.IsNullOrWhiteSpace(metadataUrl) && string.IsNullOrWhiteSpace(metadataPath))
        {
            logger.LogInformation("No Saml:IdpMetadataUrl or Saml:IdpMetadataXmlPath configured - SAML metadata/ACS endpoints are up, but login will fail until an IdP is configured.");
            return saml2Config;
        }

        try
        {
            var idpDescriptor = new EntityDescriptor();
            if (!string.IsNullOrWhiteSpace(metadataPath))
            {
                idpDescriptor.ReadIdPSsoDescriptorFromFile(metadataPath);
            }
            else
            {
                // ReadIdPSsoDescriptorFromUrl (sync) is obsolete (uses WebClient); this factory
                // runs before the app's DI container exists, so a throwaway HttpClientFactory is
                // built just for this one bootstrap-time metadata fetch.
                using var httpServices = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                var httpClientFactory = httpServices.GetRequiredService<IHttpClientFactory>();
                idpDescriptor.ReadIdPSsoDescriptorFromUrlAsync(httpClientFactory, new Uri(metadataUrl!))
                    .GetAwaiter().GetResult();
            }

            if (idpDescriptor.IdPSsoDescriptor is not null)
            {
                var sso = idpDescriptor.IdPSsoDescriptor.SingleSignOnServices.FirstOrDefault();
                if (sso is not null)
                {
                    saml2Config.SingleSignOnDestination = sso.Location;
                }

                foreach (var cert in idpDescriptor.IdPSsoDescriptor.SigningCertificates)
                {
                    saml2Config.SignatureValidationCertificates.Add(cert);
                }

                saml2Config.AllowedIssuer = idpDescriptor.EntityId;
                logger.LogInformation("Loaded SAML IdP metadata for issuer {Issuer}", idpDescriptor.EntityId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load SAML IdP metadata from {Url}{Path}. SAML login will fail until this is fixed.", metadataUrl, metadataPath);
        }

        return saml2Config;
    }
}
