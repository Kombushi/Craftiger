namespace Craftiger.Builder.Models;

/// <summary>A machine that can run a recipe, by canonical item id.</summary>
/// <param name="Tier">Input-voltage tier when the machine is a tiered block, else null.</param>
public sealed record RecipeMachine(string ItemId, bool Multiblock, int? Tier);
