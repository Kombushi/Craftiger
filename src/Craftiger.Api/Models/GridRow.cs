namespace Craftiger.Api.Models;

/// <summary>A <c>recipe_grid</c> row of planner.sqlite as read at load.</summary>
internal sealed record GridRow(string RecipeId, long Cell, long Slot);
