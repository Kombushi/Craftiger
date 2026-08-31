using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Craftiger.Api.Models;

namespace Craftiger.Api.Services;

/// <summary>Canonical request text hashed into cache ids, so identical settings land on the same entry regardless of serialization order.</summary>
internal static class CacheKeys
{
    /// <summary>The cost-solve settings every cache id starts from: garage, price base and weights.</summary>
    public static StringBuilder Settings(SolveRequest request)
    {
        var canonical = new StringBuilder();
        canonical.Append("b=").Append(request.B.ToString("R", CultureInfo.InvariantCulture));
        canonical.Append(";default=").Append(request.Garage.DefaultTier);
        Append(canonical, "machines", (request.Garage.Machines ?? [])
            .Select(m => $"{m.Key}={m.Value?.ToString() ?? "none"}"));
        Append(canonical, "built", request.Garage.BuiltMultiblocks ?? []);
        Append(canonical, "coils", (request.Garage.Coils ?? []).Select(c => $"{c.Key}={c.Value}"));
        Append(canonical, "weights", (request.Weights ?? [])
            .Select(w => $"{w.Key}={w.Value.ToString("R", CultureInfo.InvariantCulture)}"));
        return canonical;
    }

    public static void Append(StringBuilder canonical, string label, IEnumerable<string> parts)
    {
        canonical.Append(';').Append(label).Append('=');
        canonical.AppendJoin(',', parts.Order(StringComparer.Ordinal));
    }

    public static string Hash(StringBuilder canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..32];
}
