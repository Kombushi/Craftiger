namespace Craftiger.Builder.Models.Planner;

/// <summary>One recipe over canonical items; Tier is the voltage tier as GregTech labels it, before any multiblock allowance.</summary>
public sealed record PlannerRecipe(
    string Id,
    string Machine,
    int Tier,
    int? Heat,
    long DurationTicks,
    long EuT,
    long Amps,
    IReadOnlyDictionary<string, long> Inputs,
    IReadOnlyList<PlannerChoice> Choices,
    IReadOnlyList<PlannerOutput> Outputs,
    IReadOnlyList<RecipeMachine> Machines,
    IReadOnlyList<IReadOnlyList<string>> InputSlotAlternatives,
    bool RequiresCleanroom,
    bool RequiresLowGravity,
    bool EraOnly = false)
{
    /// <summary>Tool, mold and circuit slots the recipe needs in place but never consumes; only their wearing tools reach the solver.</summary>
    public IReadOnlyList<PlannerCatalystSlot> Catalysts { get; init; } = [];

    /// <summary>The filled cells of a shaped crafting recipe, null for shapeless and machine recipes or when netting removed a cell's ingredient.</summary>
    public IReadOnlyList<PlannerGridCell>? Grid { get; init; }

    public OverclockMode Overclock { get; init; } = OverclockMode.Standard;

    /// <summary>Whether the voltage is the hatch's own, so no multiblock allowance lowers the tier.</summary>
    public bool ExactTier { get; init; }

    /// <summary>Which engines read the recipe; anything but None is invisible to pricing, eras and the crafting tab.</summary>
    public RecipeScope Scope { get; init; } = RecipeScope.None;

    /// <summary>Whether the recipe consumes no input at all: a catalyst-only run that conjures its outputs.</summary>
    public bool ConsumesNothing => Inputs.Count == 0 && Choices.Count == 0;

    /// <summary>Every consumed stack, flat inputs first and then each choice's alternatives.</summary>
    public IEnumerable<(string ItemId, long Amount)> Ingredients =>
        Inputs.Select(input => (input.Key, input.Value))
            .Concat(Choices.SelectMany(choice => choice.Alternatives));

    /// <summary>The priced slots: each flat input alone, then each choice with its alternatives.</summary>
    public IEnumerable<IReadOnlyList<(string ItemId, long Amount)>> Slots =>
        Inputs.Select(input => (IReadOnlyList<(string, long)>)[(input.Key, input.Value)])
            .Concat(Choices.Select(choice => choice.Alternatives));

    /// <summary>Multiblocks run recipes one tier above their hatches, so serving this recipe on one costs a tier less.</summary>
    public int BestCaseTier =>
        Machines.Count == 0 ? Tier : Machines.Min(machine => TierOn(machine));

    /// <summary>What the garage must reach with no multiblock installed; a multiblock-only map's own discounted tier is the requirement.</summary>
    public int SingleBlockTier => HasSingleBlock ? Tier : BestCaseTier;

    /// <summary>What the garage must reach once the map's multiblock is installed; null when owning one lowers nothing.</summary>
    public int? MultiblockTier =>
        HasSingleBlock && Machines.Any(machine => machine.Multiblock) && BestCaseTier < Tier
            ? BestCaseTier
            : null;

    public bool HasSingleBlock =>
        Machines.Count == 0 || Machines.Any(machine => !machine.Multiblock);

    /// <summary>The voltage tier this recipe runs at on one machine; a heat recipe's coil gate never takes the allowance.</summary>
    public int TierOn(RecipeMachine machine) =>
        machine.Multiblock && !ExactTier && Tier > 0 ? Math.Max(1, Tier - 1) : Tier;

    /// <summary>Steam machines burn fuel for their map's LV-and-below heat-less recipes, so no voltage gates them.</summary>
    public bool RunsOnSteam(RecipeMachine machine) => machine.Steam && Tier == 1 && Heat is null;

    /// <summary>The voltage a machine must be powerable at to run this recipe: none at all on steam.</summary>
    public int VoltageTierOn(RecipeMachine machine) => RunsOnSteam(machine) ? 0 : TierOn(machine);
}
