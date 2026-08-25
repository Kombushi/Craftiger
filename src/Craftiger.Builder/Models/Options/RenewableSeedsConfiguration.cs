namespace Craftiger.Builder.Models.Options;

/// <summary>The curated world-source seed names; farm and mob seeds derive from leaf classes and mob drops instead.</summary>
public sealed record RenewableSeedsConfiguration
{
    public required IReadOnlyList<string> WorldSeedNames { get; init; }
}
