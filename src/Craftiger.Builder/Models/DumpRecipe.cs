namespace Craftiger.Builder.Models;

/// <summary><paramref name="Shapeless"/> is the recipe type's own flag: a shaped type keys
/// its item inputs by grid cell.</summary>
public sealed record DumpRecipe(string Id, string Machine, string Category, string RecipeTypeId, bool Shapeless);
