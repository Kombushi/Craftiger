namespace Craftiger.Builder.Models.Dump;

/// <summary>A GregTech recipe map and the machines that run its recipes.</summary>
public sealed record DumpRecipeMap(
    string UnlocalizedName,
    string Name,
    int Amperage,
    bool HasSingleBlock,
    bool HasMultiBlock,
    bool IsFuel,
    IReadOnlyList<DumpRecipeMapMachine> Machines);
