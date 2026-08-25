namespace Craftiger.Builder.Models.Planner;

/// <summary>The item a fraction leaf's weight divides from.</summary>
public sealed record ItemParent(string ParentItemId, double Divisor);
