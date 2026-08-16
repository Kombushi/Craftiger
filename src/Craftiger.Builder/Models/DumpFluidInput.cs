namespace Craftiger.Builder.Models;

/// <summary>One fluid input slot; a group with several stacks accepts any one of them.</summary>
public sealed record DumpFluidInput(string RecipeId, IReadOnlyList<(string FluidId, long Amount)> Members);
