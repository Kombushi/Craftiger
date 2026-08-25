namespace Craftiger.Builder.Models.Dump;

/// <summary>Item identity beyond the registry: oredict names, GT's unification verdicts and prefix flags, containers, composition.</summary>
public sealed record DumpOredictSet(
    IReadOnlyList<DumpOredictEntry> Oredict,
    IReadOnlyDictionary<string, string> UnifiedOredictTargets,
    IReadOnlySet<string> UnificationBlacklist,
    OrePrefixIndex OrePrefixes,
    IReadOnlyDictionary<string, string> ItemContainers,
    ItemDataIndex ItemData);
