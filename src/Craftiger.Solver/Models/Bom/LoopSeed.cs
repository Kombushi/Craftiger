namespace Craftiger.Solver.Models.Bom;

/// <summary>The one outside unit that starts a loop: which member, through which recipe, with which alternative picked per slot.</summary>
public sealed record LoopSeed(int Item, int Recipe, int[] Picks);
