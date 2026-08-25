namespace Craftiger.Builder.Models.Dump;

/// <summary>An oredict name split into its registered prefix and the material behind it.</summary>
public sealed record OrePrefixMatch(DumpOrePrefix Prefix, string Material);
