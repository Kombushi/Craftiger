namespace Craftiger.Builder.Models.Dump;

/// <summary>A material and how much of it, in GT material units.</summary>
public sealed record DumpMaterialAmount(string Material, long Amount);
