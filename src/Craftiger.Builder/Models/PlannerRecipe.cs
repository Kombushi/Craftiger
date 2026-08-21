namespace Craftiger.Builder.Models;

/// <param name="Tier">Voltage tier as GregTech labels it, before any multiblock allowance.</param>
public sealed record PlannerRecipe(
    string Id,
    string Machine,
    int Tier,
    int? Heat,
    long DurationTicks,
    long EuT,
    Dictionary<string, long> Inputs,
    IReadOnlyList<PlannerChoice> Choices,
    List<PlannerOutput> Outputs,
    IReadOnlyList<RecipeMachine> Machines,
    IReadOnlyList<IReadOnlyList<string>> InputSlotAlternatives,
    bool RequiresCleanroom,
    bool EraOnly = false)
{
    /// <summary>Tool, mold, and circuit slots the recipe needs in place but never consumes —
    /// never priced and never era-gated; only whether a slot holds a wearing tool reaches the
    /// solver, to break exact ties.</summary>
    public IReadOnlyList<PlannerCatalystSlot> Catalysts { get; init; } = [];

    /// <summary>The filled cells of a shaped crafting recipe, each naming the input slot it
    /// holds; null for shapeless and machine recipes, and for a shaped recipe whose cell lost
    /// its ingredient to netting, which then ships no shape at all.</summary>
    public IReadOnlyList<PlannerGridCell>? Grid { get; init; }

    /// <summary>The tier of the best machine for the job: multiblocks run recipes one tier
    /// above their hatches, so serving this recipe on one costs a tier less.</summary>
    public int BestCaseTier =>
        Machines.Count == 0 ? Tier : Machines.Min(machine => TierOn(machine));

    /// <summary>What the garage must reach with no multiblock installed. A map served only by
    /// multiblocks offers nothing else, so there its own discounted tier is the requirement.</summary>
    public int SingleBlockTier => HasSingleBlock ? Tier : BestCaseTier;

    /// <summary>What the garage must reach once the map's multiblock is installed, where owning
    /// one lowers the bar. Null when the map has no multiblock, or nothing but multiblocks.</summary>
    public int? MultiblockTier =>
        HasSingleBlock && Machines.Any(machine => machine.Multiblock) && BestCaseTier < Tier
            ? BestCaseTier
            : null;

    public bool HasSingleBlock =>
        Machines.Count == 0 || Machines.Any(machine => !machine.Multiblock);

    /// <summary>The voltage tier this recipe runs at on one machine. The coil gate of a
    /// heat recipe is a material requirement, so it never takes the multiblock allowance.</summary>
    public int TierOn(RecipeMachine machine) =>
        machine.Multiblock && Tier > 0 ? Math.Max(1, Tier - 1) : Tier;
}
