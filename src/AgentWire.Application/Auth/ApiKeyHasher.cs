using System;
using System.Security.Cryptography;
using System.Text;

namespace AgentWire.Application.Auth;

public static class ApiKeyHasher
{
    private const string Prefix = "aw_live_";

    public static (string RawKey, string Prefix) GenerateRawKey()
    {
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", string.Empty)
            .Replace("/", string.Empty)
            .Replace("=", string.Empty);
        var rawKey = Prefix + randomPart;
        return (rawKey, rawKey[..System.Math.Min(rawKey.Length, 12)]);
    }

    public static string Hash(string rawKey)
    {
        var bytes = Encoding.UTF8.GetBytes(rawKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
