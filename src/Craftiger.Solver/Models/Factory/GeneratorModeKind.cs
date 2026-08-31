namespace Craftiger.Solver.Models.Factory;

/// <summary>What a generator-mode row consumes: an output-multiplying booster, lubricant upkeep, a flat upkeep, or a reactor coolant or excited liquid.</summary>
public enum GeneratorModeKind
{
    Booster,
    Lubricant,
    Upkeep,
    Coolant,
    Excited,
}
