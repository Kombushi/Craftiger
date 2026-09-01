namespace Craftiger.Builder.Models.Dump;

/// <summary>A combustion engine's per-class constants, read off its code by the exporter.</summary>
public sealed record DumpEngine(
    string ItemId,
    int NominalOutput,
    string BoosterFluidId,
    string LubricantFluidId,
    int AdditiveFactor,
    int EfficiencyUnboosted,
    int EfficiencyBoosted);
