namespace Craftiger.Solver.Models.Graph;

/// <summary>The item a fraction leaf's weight divides from.</summary>
public sealed record ItemParentLink(string ParentItemId, double Divisor);
