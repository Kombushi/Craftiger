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

    private bool HasSingleBlock =>
        Machines.Count == 0 || Machines.Any(machine => !machine.Multiblock);

    /// <summary>The voltage tier this recipe runs at on one machine. The coil gate of a
    /// heat recipe is a material requirement, so it never takes the multiblock allowance.</summary>
    public int TierOn(RecipeMachine machine) =>
        machine.Multiblock && Tier > 0 ? Math.Max(1, Tier - 1) : Tier;
}
