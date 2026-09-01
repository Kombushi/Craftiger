namespace Craftiger.Builder.Models.Dump;

/// <summary>One registered machine with its Java class — including machines serving no recipe map.</summary>
public sealed record DumpMachine(string ItemId, string MachineClass, int? Tier, bool Multiblock, bool Steam);
