namespace Craftiger.Solver.Models.Factory;

/// <summary>What a factory target constrains: an output rate, an input rate to absorb, or a net energy export.</summary>
public enum FactoryTargetKind
{
    Produce,
    Consume,
    Energy,
}
