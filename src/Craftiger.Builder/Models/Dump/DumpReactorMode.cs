namespace Craftiger.Builder.Models.Dump;

/// <summary>One fluid a reactor drinks each second; COOLANT factors are percentages, EXCITED factors multipliers.</summary>
public sealed record DumpReactorMode(string MachineItemId, string Kind, string FluidId, int AmountPerSecond, int? Factor);
