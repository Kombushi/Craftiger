namespace Craftiger.Solver.Models;

/// <summary>One layer of the factory's lexicographic objective: priced leaf purchases, total
/// machine energy draw, or busy-machine time.</summary>
public enum FactoryObjective
{
    Resource,
    Energy,
    Machines,
}
