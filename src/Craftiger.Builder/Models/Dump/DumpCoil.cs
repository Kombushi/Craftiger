namespace Craftiger.Builder.Models.Dump;

/// <summary>A heating coil casing: its block item, display name and heat capacity.</summary>
public sealed record DumpCoil(string ItemId, string Name, int Heat);
