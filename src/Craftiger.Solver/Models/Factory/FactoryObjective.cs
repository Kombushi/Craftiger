namespace Craftiger.Solver.Models.Factory;

/// <summary>One layer of the factory's lexicographic objective: priced leaf purchases, machine energy draw, or busy-machine time.</summary>
public enum FactoryObjective
{
    Resource,
    Energy,
    Machines,
}
