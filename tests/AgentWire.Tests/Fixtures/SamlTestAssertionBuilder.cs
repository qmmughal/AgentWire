using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace AgentWire.Tests.Fixtures;

/// <summary>
/// Acts as a stub IdP: generates its own signing certificate and hand-builds/signs a
/// minimal-but-valid SAML2 &lt;samlp:Response&gt; the same shape a real IdP would send,
/// so SamlAcsTests can exercise the SP's own signature/condition validation without a
/// live external IdP. This proves the SP-side pipeline works; it does not prove
/// interoperability with any specific real-world IdP.
/// </summary>
public static class SamlTestAssertionBuilder
{
    public static X509Certificate2 CreateSigningCertificate(string subject = "CN=Fake Test IdP")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        // Re-import with the private key exportable/usable for signing operations below.
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, "test"), "test", X509KeyStorageFlags.Exportable);
    }

    public static string BuildSignedResponseBase64(
        X509Certificate2 signingCertificate,
        string idpIssuer,
        string spAudience,
        string acsDestination,
        string subjectEmail)
    {
        const string samlpNs = "urn:oasis:names:tc:SAML:2.0:protocol";
        const string samlNs = "urn:oasis:names:tc:SAML:2.0:assertion";
        var now = DateTime.UtcNow;
        var notBefore = now.AddMinutes(-5);
        var notOnOrAfter = now.AddMinutes(10);
        var responseId = "_" + Guid.NewGuid().ToString("N");
        var assertionId = "_" + Guid.NewGuid().ToString("N");
        string Fmt(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var doc = new XmlDocument { PreserveWhitespace = true };
        var response = doc.CreateElement("samlp", "Response", samlpNs);
        response.SetAttribute("ID", responseId);
        response.SetAttribute("Version", "2.0");
        response.SetAttribute("IssueInstant", Fmt(now));
        response.SetAttribute("Destination", acsDestination);
        doc.AppendChild(response);

        var responseIssuer = doc.CreateElement("saml", "Issuer", samlNs);
        responseIssuer.InnerText = idpIssuer;
        response.AppendChild(responseIssuer);

        var status = doc.CreateElement("samlp", "Status", samlpNs);
        var statusCode = doc.CreateElement("samlp", "StatusCode", samlpNs);
        statusCode.SetAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success");
        status.AppendChild(statusCode);
        response.AppendChild(status);

        var assertion = doc.CreateElement("saml", "Assertion", samlNs);
        assertion.SetAttribute("ID", assertionId);
        assertion.SetAttribute("Version", "2.0");
        assertion.SetAttribute("IssueInstant", Fmt(now));
        response.AppendChild(assertion);

        var assertionIssuer = doc.CreateElement("saml", "Issuer", samlNs);
        assertionIssuer.InnerText = idpIssuer;
        assertion.AppendChild(assertionIssuer);

        var subject = doc.CreateElement("saml", "Subject", samlNs);
        var nameId = doc.CreateElement("saml", "NameID", samlNs);
        nameId.SetAttribute("Format", "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress");
        nameId.InnerText = subjectEmail;
        subject.AppendChild(nameId);
        var subjectConfirmation = doc.CreateElement("saml", "SubjectConfirmation", samlNs);
        subjectConfirmation.SetAttribute("Method", "urn:oasis:names:tc:SAML:2.0:cm:bearer");
        var subjectConfirmationData = doc.CreateElement("saml", "SubjectConfirmationData", samlNs);
        subjectConfirmationData.SetAttribute("Recipient", acsDestination);
        subjectConfirmationData.SetAttribute("NotOnOrAfter", Fmt(notOnOrAfter));
        subjectConfirmation.AppendChild(subjectConfirmationData);
        subject.AppendChild(subjectConfirmation);
        assertion.AppendChild(subject);

        var conditions = doc.CreateElement("saml", "Conditions", samlNs);
        conditions.SetAttribute("NotBefore", Fmt(notBefore));
        conditions.SetAttribute("NotOnOrAfter", Fmt(notOnOrAfter));
        var audienceRestriction = doc.CreateElement("saml", "AudienceRestriction", samlNs);
        var audience = doc.CreateElement("saml", "Audience", samlNs);
        audience.InnerText = spAudience;
        audienceRestriction.AppendChild(audience);
        conditions.AppendChild(audienceRestriction);
        assertion.AppendChild(conditions);

        var authnStatement = doc.CreateElement("saml", "AuthnStatement", samlNs);
        authnStatement.SetAttribute("AuthnInstant", Fmt(now));
        var authnContext = doc.CreateElement("saml", "AuthnContext", samlNs);
        var authnContextClassRef = doc.CreateElement("saml", "AuthnContextClassRef", samlNs);
        authnContextClassRef.InnerText = "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport";
        authnContext.AppendChild(authnContextClassRef);
        authnStatement.AppendChild(authnContext);
        assertion.AppendChild(authnStatement);

        var attributeStatement = doc.CreateElement("saml", "AttributeStatement", samlNs);
        var emailAttribute = doc.CreateElement("saml", "Attribute", samlNs);
        emailAttribute.SetAttribute("Name", "email");
        var emailAttributeValue = doc.CreateElement("saml", "AttributeValue", samlNs);
        emailAttributeValue.InnerText = subjectEmail;
        emailAttribute.AppendChild(emailAttributeValue);
        attributeStatement.AppendChild(emailAttribute);
        assertion.AppendChild(attributeStatement);

        SignElement(doc, assertion, assertionId, signingCertificate, insertAfter: assertionIssuer);

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(doc.OuterXml));
    }

    private static void SignElement(XmlDocument doc, XmlElement elementToSign, string elementId, X509Certificate2 cert, XmlElement insertAfter)
    {
        var signedXml = new SignedXml(doc)
        {
            SigningKey = cert.GetRSAPrivateKey()
        };
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var reference = new Reference("#" + elementId)
        {
            DigestMethod = SignedXml.XmlDsigSHA256Url
        };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();
        var imported = (XmlElement)doc.ImportNode(signatureElement, true);
        elementToSign.InsertAfter(imported, insertAfter);
    }
}
