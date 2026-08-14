using System.Text.RegularExpressions;
using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.Repositories;

public sealed partial class DumpRepository : IDumpRepository
{
    [GeneratedRegex("§.")]
    private static partial Regex Formatting();

    [GeneratedRegex(@"Voltage IN:\s*([\d,]+)")]
    private static partial Regex VoltageIn();

    public Dump Read(string dumpPath)
    {
        using var db = new SqliteConnection($"Data Source={dumpPath};Mode=ReadOnly");
        db.Open();

        var items = db.Query<DumpItem>("""
            SELECT ID AS Id, LOCALIZED_NAME AS Name, MOD_ID AS ModId,
                INTERNAL_NAME AS InternalName, IMAGE_FILE_PATH AS ImagePath
            FROM ITEM
            """).ToDictionary(i => i.Id);

        var fluids = db.Query<DumpFluid>("""
            SELECT ID AS Id, LOCALIZED_NAME AS Name, MOD_ID AS ModId,
                INTERNAL_NAME AS InternalName, IMAGE_FILE_PATH AS ImagePath
            FROM FLUID
            """).ToDictionary(f => f.Id);

        var handlerItems = new Dictionary<string, List<string>>();
        foreach (var (typeId, iconId) in db.Query<(string, string)>(
            """SELECT RECIPE_TYPE_ID, ICON_ID FROM RECIPE_TYPE_ITEM WHERE ICON_ID IS NOT NULL"""))
        {
            Add(handlerItems, typeId, iconId);
        }

        var recipes = new List<DumpRecipe>();
        foreach (var (id, type, category, typeId) in db.Query<(string, string, string, string)>("""
            SELECT r.ID, rt.TYPE, rt.CATEGORY, rt.ID
            FROM RECIPE r JOIN RECIPE_TYPE rt ON rt.ID = r.RECIPE_TYPE_ID
            """))
        {
            recipes.Add(new DumpRecipe(id, type, category, handlerItems.GetValueOrDefault(typeId)?.Count ?? 0, typeId));
        }

        // coil_heat metadata is authoritative; RECIPE_SPECIAL_VALUE holds the same number for EBF maps.
        var gt = new Dictionary<string, DumpGtData>();
        foreach (var r in db.Query<(string Id, long Voltage, long Amperage, long Duration, long? Heat, string? TierLabel, long? Cleanroom)>("""
            SELECT g.RECIPE_ID, g.VOLTAGE, g.AMPERAGE, g.DURATION, m.METADATA_VALUE, g.VOLTAGE_TIER, g.REQUIRES_CLEANROOM
            FROM GREG_TECH_RECIPE g
            LEFT JOIN GREG_TECH_RECIPE_METADATA m ON m.GREG_TECH_RECIPE_ID = g.ID AND m.METADATA_KEY = 'coil_heat'
            """))
        {
            gt[r.Id] = new DumpGtData(
                r.Id, r.Voltage, r.Amperage, r.Duration, (int?)r.Heat, r.TierLabel, r.Cleanroom is not (null or 0));
        }

        var groupStacks = new Dictionary<string, List<DumpItemStack>>();
        foreach (var (groupId, itemId, size) in db.Query<(string, string, long)>(
            """SELECT ITEM_GROUP_ID, ITEM_STACKS_ITEM_ID, ITEM_STACKS_STACK_SIZE FROM ITEM_GROUP_ITEM_STACKS"""))
        {
            Add(groupStacks, groupId, new DumpItemStack(itemId, size));
        }

        var oredict = db.Query<(string OredictName, string GroupId)>(
            """SELECT NAME, ITEM_GROUP_ID FROM ORE_DICTIONARY""").ToList();

        var itemInputs = new Dictionary<string, List<(long Slot, string GroupId)>>();
        foreach (var (recipeId, slot, groupId) in db.Query<(string, long, string)>(
            """SELECT RECIPE_ID, ITEM_INPUTS_KEY, ITEM_INPUTS_ID FROM RECIPE_ITEM_GROUP"""))
        {
            Add(itemInputs, recipeId, (slot, groupId));
        }

        var itemOutputs = new Dictionary<string, List<DumpItemOutput>>();
        foreach (var r in db.Query<(string RecipeId, string ItemId, long Size, double? Chance, long? Slot)>("""
            SELECT RECIPE_ID, ITEM_OUTPUTS_VALUE_ITEM_ID, ITEM_OUTPUTS_VALUE_STACK_SIZE, ITEM_OUTPUTS_VALUE_PROBABILITY, ITEM_OUTPUTS_KEY
            FROM RECIPE_ITEM_OUTPUTS WHERE ITEM_OUTPUTS_VALUE_ITEM_ID IS NOT NULL
            """))
        {
            Add(itemOutputs, r.RecipeId, new DumpItemOutput(r.RecipeId, r.ItemId, r.Size, r.Chance ?? 1.0, r.Slot ?? 0));
        }

        // Fluid input groups are single-stack in practice; excess stacks would be alternatives and are ignored.
        var fluidGroupStack = new Dictionary<string, (string FluidId, long Amount)>();
        foreach (var (groupId, fluidId, amount) in db.Query<(string, string, long)>(
            """SELECT FLUID_GROUP_ID, FLUID_STACKS_FLUID_ID, FLUID_STACKS_AMOUNT FROM FLUID_GROUP_FLUID_STACKS"""))
        {
            fluidGroupStack.TryAdd(groupId, (fluidId, amount));
        }

        var fluidInputs = new Dictionary<string, List<DumpFluidInput>>();
        foreach (var (recipeId, groupId) in db.Query<(string, string)>(
            """SELECT RECIPE_ID, FLUID_INPUTS_ID FROM RECIPE_FLUID_GROUP"""))
        {
            if (fluidGroupStack.TryGetValue(groupId, out var s))
            {
                Add(fluidInputs, recipeId, new DumpFluidInput(recipeId, s.FluidId, s.Amount));
            }
        }

        var fluidOutputs = new Dictionary<string, List<DumpFluidOutput>>();
        foreach (var r in db.Query<(string RecipeId, string FluidId, long Amount, double? Chance)>("""
            SELECT RECIPE_ID, FLUID_OUTPUTS_VALUE_FLUID_ID, FLUID_OUTPUTS_VALUE_AMOUNT, FLUID_OUTPUTS_VALUE_PROBABILITY
            FROM RECIPE_FLUID_OUTPUTS WHERE FLUID_OUTPUTS_VALUE_FLUID_ID IS NOT NULL
            """))
        {
            Add(fluidOutputs, r.RecipeId, new DumpFluidOutput(r.RecipeId, r.FluidId, r.Amount, r.Chance ?? 1.0));
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

        var machineVoltageTiers = new Dictionary<string, int>();
        foreach (var (itemId, tooltip) in db.Query<(string, string)>(
            """SELECT ITEM_ID, TOOLTIP FROM ITEM_TOOLTIP WHERE TOOLTIP LIKE '%Voltage IN:%'"""))
        {
            var match = VoltageIn().Match(Formatting().Replace(tooltip, ""));
            if (match.Success && long.TryParse(match.Groups[1].Value.Replace(",", ""), out var voltage) && voltage > 0)
            {
                machineVoltageTiers[itemId] = TierLadder.VoltageTier(voltage);
            }
        }

        var worldgenOres = new List<DumpWorldgenOre>();
        foreach (var r in db.Query<(string ItemId, string? MaterialName, string Dimension, int Tier)>("""
            SELECT O.ORES_ITEM_ID, O.ORES_MATERIAL_NAME, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_ORE_VEIN_ORES O
            JOIN GREG_TECH_ORE_VEIN V ON V.ID = O.GREG_TECH_ORE_VEIN_ID AND V.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_ORE_VEIN_DIMENSIONS VD ON VD.GREG_TECH_ORE_VEIN_ID = V.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = VD.DIMENSIONS_DIMENSION_ABBREVIATION
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, r.MaterialName, r.Dimension, r.Tier, IsDrop: false));
        }
        foreach (var r in db.Query<(string ItemId, string? MaterialName, string Dimension, int Tier)>("""
            SELECT B.BLOCKS_ITEM_ID, S.MATERIAL_NAME, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_SMALL_ORE_BLOCKS B
            JOIN GREG_TECH_SMALL_ORE S ON S.ID = B.GREG_TECH_SMALL_ORE_ID AND S.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_SMALL_ORE_DIMENSIONS SD ON SD.GREG_TECH_SMALL_ORE_ID = S.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = SD.DIMENSIONS_DIMENSION_ABBREVIATION
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, r.MaterialName, r.Dimension, r.Tier, IsDrop: false));
        }
        foreach (var r in db.Query<(string ItemId, string Dimension, int Tier)>("""
            SELECT P.DROPS_ITEM_ID, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_SMALL_ORE_DROPS P
            JOIN GREG_TECH_SMALL_ORE S ON S.ID = P.GREG_TECH_SMALL_ORE_ID AND S.ENABLED_BY_DEFAULT != 0
            JOIN GREG_TECH_SMALL_ORE_DIMENSIONS SD ON SD.GREG_TECH_SMALL_ORE_ID = S.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = SD.DIMENSIONS_DIMENSION_ABBREVIATION
            """))
        {
            worldgenOres.Add(new DumpWorldgenOre(r.ItemId, MaterialName: null, r.Dimension, r.Tier, IsDrop: true));
        }

        var metadata = db.Query<(string Version, long CreatedMillis)>(
            """SELECT VERSION, CREATION_TIME_MILLIS FROM METADATA LIMIT 1""").FirstOrDefault();

        return new Dump
        {
            Items = items,
            Fluids = fluids,
            Recipes = recipes,
            GtByRecipeId = gt,
            GroupStacks = groupStacks,
            Oredict = oredict,
            ItemInputsByRecipe = itemInputs,
            ItemOutputsByRecipe = itemOutputs,
            FluidInputsByRecipe = fluidInputs,
            FluidOutputsByRecipe = fluidOutputs,
            ContainersByItemId = containers,
            HandlerItemsByRecipeTypeId = handlerItems,
            WorldgenOres = worldgenOres,
            MachineVoltageTiers = machineVoltageTiers,
            ExporterVersion = metadata.Version ?? "unknown",
            ExportedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.CreatedMillis)
        };
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }
        list.Add(value);
    }
}
