namespace Craftiger.Builder.Models.Dump;

/// <summary>Shapeless is the recipe type's own flag: a shaped type keys its item inputs by grid cell.</summary>
public sealed record DumpRecipe(string Id, string Machine, string Category, string RecipeTypeId, bool Shapeless);
