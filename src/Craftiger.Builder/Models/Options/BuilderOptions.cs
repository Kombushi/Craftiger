namespace Craftiger.Builder.Models.Options;

/// <summary>Inputs and outputs of one builder run.</summary>
public sealed record BuilderOptions
{
    /// <summary>The NESQL dump, converted to SQLite.</summary>
    public required string DumpPath { get; init; }

    /// <summary>Directory the three artifacts are written to.</summary>
    public required string OutputDir { get; init; }

    /// <summary>Modpack version stamped into the artifacts.</summary>
    public required string PackVersion { get; init; }

    /// <summary>Icon archive exported beside the dump; the atlas is skipped when it is missing.</summary>
    public required string ImagesPath { get; init; }

    /// <summary>When set, the run prints this item's era derivation and writes nothing.</summary>
    public string? ExplainItem { get; init; }
}