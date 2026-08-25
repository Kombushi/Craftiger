namespace Craftiger.Builder.Models.Dump;

/// <summary>Which exporter wrote the dump, and when.</summary>
public sealed record DumpMetadata(string ExporterVersion, DateTimeOffset ExportedAt);
