using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgentWire.Core.Enums;

namespace AgentWire.Application.Security;

/// <summary>
/// Rule-based (not ML) prompt-injection and PII detection. Deliberately simple and
/// documented as such - see docs/roadmap.md for the "Custom Security Rules Engine"
/// item this scanner does NOT implement (rules here are hardcoded, not user-configurable).
/// </summary>
public sealed class PacketScanner : IPacketScanner
{
    private static readonly (string RuleId, Regex Pattern)[] InjectionRules =
    [
        ("PI-001", new Regex(@"ignore\s+(all|any|previous|prior|the\s+above)\s+instructions?", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-002", new Regex(@"disregard\s+(?:all|any|previous|prior)\s+(?:(?:all|any|previous|prior)\s+)?(instructions|rules|prompt)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-003", new Regex(@"you\s+are\s+now\s+(in\s+)?(developer|dan|jailbreak)\s+mode", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-004", new Regex(@"reveal\s+(your\s+|the\s+)?(system\s+prompt|instructions)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-005", new Regex(@"act\s+as\s+if\s+you\s+(have\s+no|had\s+no)\s+(restrictions|guidelines|rules)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-006", new Regex(@"\bDAN\b.{0,20}(mode|prompt)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-007", new Regex(@"pretend\s+(you\s+are|to\s+be)\s+(an?\s+)?(unfiltered|unrestricted|uncensored)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-008", new Regex(@"\bBEGIN\s+(SYSTEM|ADMIN)\s+(PROMPT|OVERRIDE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PI-009", new Regex(@"(new\s+instructions\s*:|system\s*:\s*override)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\b(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex CreditCardCandidatePattern = new(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled);
    private static readonly Regex SsnPattern = new(@"\b(\d{3})-(\d{2})-(\d{4})\b", RegexOptions.Compiled);

    public IReadOnlyList<PacketScanFinding> Scan(string? systemPrompt, string? userPrompt, string? llmResponse)
    {
        var findings = new List<PacketScanFinding>();

        ScanField(systemPrompt, FindingLocation.SystemPrompt, findings);
        ScanField(userPrompt, FindingLocation.UserPrompt, findings);
        ScanField(llmResponse, FindingLocation.LlmResponse, findings);

        return findings;
    }

    private static void ScanField(string? text, FindingLocation location, List<PacketScanFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var (ruleId, pattern) in InjectionRules)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                findings.Add(new PacketScanFinding(
                    FindingType.PromptInjection,
                    FindingSeverity.High,
                    location,
                    ruleId,
                    MaskGeneric(match.Value)));
            }
        }

        foreach (Match match in EmailPattern.Matches(text))
        {
            findings.Add(new PacketScanFinding(
                FindingType.PiiEmail,
                FindingSeverity.Medium,
                location,
                "PII-EMAIL-001",
                MaskEmail(match.Value)));
        }

        foreach (Match match in PhonePattern.Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is 10 or 11)
            {
                findings.Add(new PacketScanFinding(
                    FindingType.PiiPhone,
                    FindingSeverity.Medium,
                    location,
                    "PII-PHONE-001",
                    MaskKeepLast(digits, 4)));
            }
        }

        foreach (Match match in CreditCardCandidatePattern.Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && LuhnValidator.IsValid(digits))
            {
                findings.Add(new PacketScanFinding(
                    FindingType.PiiCreditCard,
                    FindingSeverity.High,
                    location,
                    "PII-CC-001",
                    MaskKeepLast(digits, 4)));
            }
        }

        foreach (Match match in SsnPattern.Matches(text))
        {
            var area = match.Groups[1].Value;
            if (IsValidSsnArea(area))
            {
                var digits = area + match.Groups[2].Value + match.Groups[3].Value;
                findings.Add(new PacketScanFinding(
                    FindingType.PiiSsn,
                    FindingSeverity.High,
                    location,
                    "PII-SSN-001",
                    MaskKeepLast(digits, 4, groupOf: 3)));
            }
        }
    }

    private static bool IsValidSsnArea(string area)
    {
        var areaNum = int.Parse(area);
        return areaNum != 0 && areaNum != 666 && areaNum < 900;
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "*" + email[atIndex..];
        }

        return email[0] + new string('*', atIndex - 1) + email[atIndex..];
    }

    private static string MaskKeepLast(string digits, int keep, int groupOf = 4)
    {
        if (digits.Length <= keep)
        {
            return new string('*', digits.Length);
        }

        var maskedCount = digits.Length - keep;
        var masked = new string('*', maskedCount) + digits[^keep..];
        return string.Join("-", Chunk(masked, groupOf));
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (int i = 0; i < value.Length; i += size)
        {
            yield return value.Substring(i, System.Math.Min(size, value.Length - i));
        }
    }

    private static string MaskGeneric(string text)
    {
        if (text.Length <= 8)
        {
            return text;
        }

        return text[..4] + "..." + text[^4..];
    }
}
