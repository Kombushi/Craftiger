namespace Craftiger.Solver.Models;

/// <summary>The one outside unit that starts a loop: which member, through which recipe, with
/// which alternative picked per slot. Positions throughout.</summary>
internal sealed record LoopSeed(int Item, int Recipe, int[] Picks);
