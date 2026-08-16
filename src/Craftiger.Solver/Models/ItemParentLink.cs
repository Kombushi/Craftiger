namespace Craftiger.Solver.Models;

/// <summary>The item a fraction leaf's weight divides from.</summary>
public sealed record ItemParentLink(string ParentItemId, double Divisor);
