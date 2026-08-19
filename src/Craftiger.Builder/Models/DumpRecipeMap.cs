namespace Craftiger.Builder.Models;

/// <summary>A GregTech recipe map and the machines that run its recipes.</summary>
public sealed record DumpRecipeMap(
    string UnlocalizedName,
    string Name,
    int Amperage,
    bool HasSingleBlock,
    bool HasMultiBlock,
    bool IsFuel,
    List<DumpRecipeMapMachine> Machines);
