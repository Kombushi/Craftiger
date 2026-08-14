namespace Gtnh.Planner.Builder.Models;

/// <summary>Command-line options driving one builder run.</summary>
public sealed record BuilderOptions(
    string DumpPath,
    string OutputDir,
    string PackVersion,
    string ImagesPath,
    string? ExplainItem);
