using AgentWire.Core.Enums;

namespace AgentWire.Application.Security;

public sealed record PacketScanFinding(
    FindingType FindingType,
    FindingSeverity Severity,
    FindingLocation Location,
    string RuleId,
    string MatchedTextMasked);
