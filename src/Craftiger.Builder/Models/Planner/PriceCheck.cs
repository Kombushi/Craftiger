namespace Craftiger.Builder.Models.Planner;

/// <summary>The price check's verdict: leaves priced far below their weight, items costing nothing, and whether the walk settled — being cut short is itself a bad sign.</summary>
public sealed record PriceCheck(int Undercut, int Free, bool Converged);
