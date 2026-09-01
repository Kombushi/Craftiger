using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Repositories.DumpReaders;

public sealed class DumpRecipeReader(ILogger<DumpRecipeReader> logger) : IDumpRecipeReader
{
    public DumpRecipeSet Read(SqliteConnection db)
    {
        var handlerItems = new Dictionary<string, List<string>>();
        foreach (var (typeId, iconId) in db.Query<(string, string)>(
            """SELECT RECIPE_TYPE_ID, ICON_ID FROM RECIPE_TYPE_ITEM WHERE ICON_ID IS NOT NULL"""))
        {
            DumpQueries.Add(handlerItems, typeId, iconId);
        }

        var recipes = new List<DumpRecipe>();
        foreach (var (id, type, category, typeId, shapeless) in db.Query<(string, string, string, string, long)>("""
            SELECT r.ID, rt.TYPE, rt.CATEGORY, rt.ID, rt.SHAPELESS
            FROM RECIPE r JOIN RECIPE_TYPE rt ON rt.ID = r.RECIPE_TYPE_ID
            """))
        {
            recipes.Add(new DumpRecipe(id, type, category, typeId, shapeless != 0));
        }

        // coil_heat metadata is authoritative; RECIPE_SPECIAL_VALUE holds the same number for EBF maps.
        var categoryColumn = DumpQueries.HasColumn(db, "GREG_TECH_RECIPE", "RECIPE_CATEGORY") ? "g.RECIPE_CATEGORY" : "''";
        var specialItemJoin = DumpQueries.HasTable(db, "GREG_TECH_RECIPE_ITEM")
            ? "LEFT JOIN GREG_TECH_RECIPE_ITEM s ON s.GREG_TECH_RECIPE_ID = g.ID"
            : "LEFT JOIN (SELECT NULL AS GREG_TECH_RECIPE_ID, NULL AS SPECIAL_ITEMS_ID) s ON 0";
        var gt = new Dictionary<string, DumpGtData>();
        foreach (var r in db.Query<(string Id, long? Voltage, long Amperage, long Duration, long? Heat, string? TierLabel, long? Cleanroom, long? LowGravity, long? SpecialValue, string? AdditionalInfo, string? Category, string? SpecialItemId)>($"""
            SELECT g.RECIPE_ID, g.VOLTAGE, g.AMPERAGE, g.DURATION, m.METADATA_VALUE, g.VOLTAGE_TIER, g.REQUIRES_CLEANROOM, g.REQUIRES_LOW_GRAVITY, g.RECIPE_SPECIAL_VALUE, g.ADDITIONAL_INFO, {categoryColumn}, s.SPECIAL_ITEMS_ID
            FROM GREG_TECH_RECIPE g
            LEFT JOIN GREG_TECH_RECIPE_METADATA m ON m.GREG_TECH_RECIPE_ID = g.ID AND m.METADATA_KEY = 'coil_heat'
            {specialItemJoin}
            """))
        {
            gt[r.Id] = new DumpGtData(
                r.Voltage, r.Amperage, r.Duration, (int?)r.Heat, r.TierLabel,
                r.Cleanroom is not (null or 0), r.LowGravity is not (null or 0),
                r.SpecialValue, r.AdditionalInfo, r.Category ?? "", r.SpecialItemId);
        }
        if (categoryColumn == "''")
        {
            logger.LogWarning(
                "dump predates RECIPE_CATEGORY; recycling recipes cannot be told from production ones");
        }

        var groupStacks = new Dictionary<string, List<DumpItemStack>>();
        foreach (var (groupId, itemId, size) in db.Query<(string, string, long)>(
            """SELECT ITEM_GROUP_ID, ITEM_STACKS_ITEM_ID, ITEM_STACKS_STACK_SIZE FROM ITEM_GROUP_ITEM_STACKS"""))
        {
            DumpQueries.Add(groupStacks, groupId, new DumpItemStack(itemId, size));
        }

        var itemInputs = new Dictionary<string, List<DumpItemInput>>();
        foreach (var (recipeId, slot, groupId) in db.Query<(string, long, string)>(
            """SELECT RECIPE_ID, ITEM_INPUTS_KEY, ITEM_INPUTS_ID FROM RECIPE_ITEM_GROUP"""))
        {
            DumpQueries.Add(itemInputs, recipeId, new DumpItemInput(slot, groupId));
        }

        var itemOutputs = new Dictionary<string, List<DumpItemOutput>>();
        foreach (var r in db.Query<(string RecipeId, string ItemId, long Size, double? Chance, long? Slot)>("""
            SELECT RECIPE_ID, ITEM_OUTPUTS_VALUE_ITEM_ID, ITEM_OUTPUTS_VALUE_STACK_SIZE, ITEM_OUTPUTS_VALUE_PROBABILITY, ITEM_OUTPUTS_KEY
            FROM RECIPE_ITEM_OUTPUTS WHERE ITEM_OUTPUTS_VALUE_ITEM_ID IS NOT NULL
            """))
        {
            DumpQueries.Add(itemOutputs, r.RecipeId, new DumpItemOutput(r.ItemId, r.Size, r.Chance ?? 1.0, r.Slot ?? 0));
        }

        var fluidGroupStacks = new Dictionary<string, List<DumpFluidStack>>();
        foreach (var (groupId, fluidId, amount) in db.Query<(string, string, long)>(
            """SELECT FLUID_GROUP_ID, FLUID_STACKS_FLUID_ID, FLUID_STACKS_AMOUNT FROM FLUID_GROUP_FLUID_STACKS"""))
        {
            DumpQueries.Add(fluidGroupStacks, groupId, new DumpFluidStack(fluidId, amount));
        }

        var fluidInputs = new Dictionary<string, List<DumpFluidInput>>();
        foreach (var (recipeId, groupId) in db.Query<(string, string)>(
            """SELECT RECIPE_ID, FLUID_INPUTS_ID FROM RECIPE_FLUID_GROUP"""))
        {
            if (fluidGroupStacks.TryGetValue(groupId, out var members))
            {
                DumpQueries.Add(fluidInputs, recipeId, new DumpFluidInput(members));
            }
        }

        var fluidOutputs = new Dictionary<string, List<DumpFluidOutput>>();
        foreach (var r in db.Query<(string RecipeId, string FluidId, long Amount, double? Chance)>("""
            SELECT RECIPE_ID, FLUID_OUTPUTS_VALUE_FLUID_ID, FLUID_OUTPUTS_VALUE_AMOUNT, FLUID_OUTPUTS_VALUE_PROBABILITY
            FROM RECIPE_FLUID_OUTPUTS WHERE FLUID_OUTPUTS_VALUE_FLUID_ID IS NOT NULL
            """))
        {
            DumpQueries.Add(fluidOutputs, r.RecipeId, new DumpFluidOutput(r.FluidId, r.Amount, r.Chance ?? 1.0));
        }

        var containers = new Dictionary<string, DumpContainer>();
        foreach (var r in db.Query<(string ContainerId, string FluidId, long Amount, string EmptyItemId)>("""
            SELECT CONTAINER_ID, FLUID_STACK_FLUID_ID, FLUID_STACK_AMOUNT, EMPTY_CONTAINER_ID
            FROM FLUID_CONTAINER
            WHERE CONTAINER_ID IS NOT NULL AND FLUID_STACK_FLUID_ID IS NOT NULL AND EMPTY_CONTAINER_ID IS NOT NULL
            """))
        {
            containers.TryAdd(r.ContainerId, new DumpContainer(r.FluidId, r.Amount, r.EmptyItemId));
        }

        return new DumpRecipeSet(
            recipes, gt,
            DumpQueries.Freeze(groupStacks),
            DumpQueries.Freeze(itemInputs),
            DumpQueries.Freeze(itemOutputs),
            DumpQueries.Freeze(fluidInputs),
            DumpQueries.Freeze(fluidOutputs),
            containers,
            DumpQueries.Freeze(handlerItems));
    }
}
