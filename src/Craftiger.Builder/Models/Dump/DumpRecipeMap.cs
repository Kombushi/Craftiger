namespace Craftiger.Builder.Models.Dump;

/// <summary>A GregTech recipe map and the machines that run its recipes.</summary>
public sealed record DumpRecipeMap(
    string UnlocalizedName,
    string Name,
    int Amperage,
    bool HasSingleBlock,
    bool HasMultiBlock,
    bool IsFuel,
    IReadOnlyList<DumpRecipeMapMachine> Machines)
{
    /// <summary>Tier per byproduct slot, from where the map's electric single blocks gain output slots; null when no tier adds one.</summary>
    public IReadOnlyList<int>? ByproductSlotTiers()
    {
        var singles = Machines
            .Where(m => m is { Multiblock: false, Steam: false, Tier: not null, OutputSlots: not null })
            .ToList();
        if (singles.Count == 0)
        {
            return null;
        }

        var most = singles.Max(m => m.OutputSlots!.Value);
        if (most <= 1 || singles.Min(m => m.OutputSlots!.Value) == most)
        {
            return null;
        }

        var tiers = new int[most - 1];
        for (var slot = 1; slot < most; slot++)
        {
            tiers[slot - 1] = singles.Where(m => m.OutputSlots!.Value > slot).Min(m => m.Tier!.Value);
        }
        return tiers;
    }
}
