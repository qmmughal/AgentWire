using System.Collections.Generic;

namespace AgentWire.Application.Security;

public interface IPacketScanner
{
    IReadOnlyList<PacketScanFinding> Scan(string? systemPrompt, string? userPrompt, string? llmResponse);
}
