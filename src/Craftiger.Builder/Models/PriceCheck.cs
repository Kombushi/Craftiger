namespace Craftiger.Builder.Models;

/// <param name="Undercut">Leaves a recipe route prices far below their own weight.</param>
/// <param name="Free">Items that came out costing nothing at all.</param>
/// <param name="Converged">False when the walk was cut short, which is itself a bad sign.</param>
public sealed record PriceCheck(int Undercut, int Free, bool Converged);
