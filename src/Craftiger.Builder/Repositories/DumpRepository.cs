using System.Text.RegularExpressions;
using Dapper;
using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Repositories;

public sealed partial class DumpRepository(ILogger<DumpRepository> logger) : IDumpRepository
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
                INTERNAL_NAME AS InternalName, IMAGE_FILE_PATH AS ImagePath,
                MAX_DAMAGE AS MaxDamage, MAX_STACK_SIZE AS MaxStackSize
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
        foreach (var (id, type, category, typeId, shapeless) in db.Query<(string, string, string, string, long)>("""
            SELECT r.ID, rt.TYPE, rt.CATEGORY, rt.ID, rt.SHAPELESS
            FROM RECIPE r JOIN RECIPE_TYPE rt ON rt.ID = r.RECIPE_TYPE_ID
            """))
        {
            recipes.Add(new DumpRecipe(id, type, category, typeId, shapeless != 0));
        }

        // coil_heat metadata is authoritative; RECIPE_SPECIAL_VALUE holds the same number for EBF maps.
        var categoryColumn = HasColumn(db, "GREG_TECH_RECIPE", "RECIPE_CATEGORY") ? "g.RECIPE_CATEGORY" : "''";
        var gt = new Dictionary<string, DumpGtData>();
        foreach (var r in db.Query<(string Id, long? Voltage, long Amperage, long Duration, long? Heat, string? TierLabel, long? Cleanroom, long? LowGravity, long? SpecialValue, string? AdditionalInfo, string? Category)>($"""
            SELECT g.RECIPE_ID, g.VOLTAGE, g.AMPERAGE, g.DURATION, m.METADATA_VALUE, g.VOLTAGE_TIER, g.REQUIRES_CLEANROOM, g.REQUIRES_LOW_GRAVITY, g.RECIPE_SPECIAL_VALUE, g.ADDITIONAL_INFO, {categoryColumn}
            FROM GREG_TECH_RECIPE g
            LEFT JOIN GREG_TECH_RECIPE_METADATA m ON m.GREG_TECH_RECIPE_ID = g.ID AND m.METADATA_KEY = 'coil_heat'
            """))
        {
            gt[r.Id] = new DumpGtData(
                r.Id, r.Voltage, r.Amperage, r.Duration, (int?)r.Heat, r.TierLabel,
                r.Cleanroom is not (null or 0), r.LowGravity is not (null or 0),
                r.SpecialValue, r.AdditionalInfo, r.Category ?? "");
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
            Add(groupStacks, groupId, new DumpItemStack(itemId, size));
        }

        var oredict = db.Query<(string OredictName, string GroupId)>(
            """SELECT NAME, ITEM_GROUP_ID FROM ORE_DICTIONARY""").ToList();

        if (!HasTable(db, "GREG_TECH_ORE_DICT_UNIFICATION"))
        {
            throw new InvalidOperationException(
                "dump predates GREG_TECH_ORE_DICT_UNIFICATION; re-export with exporter 0.6.3 or later");
        }
        var unifiedOredictTargets = db.Query<(string Name, string TargetId)>(
            """SELECT NAME, TARGET_ID FROM GREG_TECH_ORE_DICT_UNIFICATION""")
            .ToDictionary(r => r.Name, r => r.TargetId);
        var unificationBlacklist = db.Query<string>(
            """SELECT ITEM_ID FROM GREG_TECH_UNIFICATION_BLACKLIST""").ToHashSet();

        if (!HasTable(db, "GREG_TECH_ORE_PREFIX"))
        {
            throw new InvalidOperationException(
                "dump predates GREG_TECH_ORE_PREFIX; re-export with exporter 0.6.3 or later");
        }
        var orePrefixes = db.Query<(string Name, bool Unifiable, bool SelfReferencing, bool MaterialBased,
            bool Container, bool Recyclable, long MaterialAmount)>("""
            SELECT NAME, UNIFIABLE, SELF_REFERENCING, MATERIAL_BASED, CONTAINER, RECYCLABLE, MATERIAL_AMOUNT
            FROM GREG_TECH_ORE_PREFIX
            """)
            .ToDictionary(r => r.Name, r => new DumpOrePrefix(
                r.Name, r.Unifiable, r.SelfReferencing, r.MaterialBased, r.Container, r.Recyclable,
                r.MaterialAmount));

        if (!HasTable(db, "ITEM_CONTAINER"))
        {
            throw new InvalidOperationException(
                "dump predates ITEM_CONTAINER; re-export with exporter 0.6.4 or later");
        }
        var itemContainers = db.Query<(string ItemId, string ContainerId)>(
            """SELECT ITEM_ID, CONTAINER_ITEM_ID FROM ITEM_CONTAINER""")
            .ToDictionary(r => r.ItemId, r => r.ContainerId);

        if (!HasTable(db, "GREG_TECH_ITEM_DATA"))
        {
            throw new InvalidOperationException(
                "dump predates GREG_TECH_ITEM_DATA; re-export with exporter 0.6.4 or later");
        }
        var itemDataByproducts = new Dictionary<string, List<(string Material, long Amount)>>();
        foreach (var (dataId, material, amount) in db.Query<(string, string, long)>("""
            SELECT GREG_TECH_ITEM_DATA_ID, BY_PRODUCTS_MATERIAL_NAME, BY_PRODUCTS_AMOUNT
            FROM GREG_TECH_ITEM_DATA_BY_PRODUCTS WHERE BY_PRODUCTS_MATERIAL_NAME IS NOT NULL
            """))
        {
            Add(itemDataByproducts, dataId, (material, amount));
        }
        var itemData = db.Query<(string Id, string ItemId, string? Prefix, string Material, long Amount)>("""
            SELECT ID, ITEM_ID, PREFIX_NAME, MATERIAL_NAME, MATERIAL_AMOUNT
            FROM GREG_TECH_ITEM_DATA WHERE MATERIAL_NAME IS NOT NULL
            """)
            .Select(r => new DumpItemData(
                r.ItemId, r.Prefix, r.Material, r.Amount,
                itemDataByproducts.GetValueOrDefault(r.Id) ?? []))
            .ToList();

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

        var fluidGroupStacks = new Dictionary<string, List<(string FluidId, long Amount)>>();
        foreach (var (groupId, fluidId, amount) in db.Query<(string, string, long)>(
            """SELECT FLUID_GROUP_ID, FLUID_STACKS_FLUID_ID, FLUID_STACKS_AMOUNT FROM FLUID_GROUP_FLUID_STACKS"""))
        {
            Add(fluidGroupStacks, groupId, (fluidId, amount));
        }

        var fluidInputs = new Dictionary<string, List<DumpFluidInput>>();
        foreach (var (recipeId, groupId) in db.Query<(string, string)>(
            """SELECT RECIPE_ID, FLUID_INPUTS_ID FROM RECIPE_FLUID_GROUP"""))
        {
            if (fluidGroupStacks.TryGetValue(groupId, out var members))
            {
                Add(fluidInputs, recipeId, new DumpFluidInput(recipeId, members));
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
            JOIN GREG_TECH_DIMENSION_STONE_TYPES ST ON ST.GREG_TECH_DIMENSION_ID = D.ID AND ST.STONE_TYPES = O.ORES_STONE_TYPE
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
            JOIN GREG_TECH_DIMENSION_STONE_TYPES ST ON ST.GREG_TECH_DIMENSION_ID = D.ID AND ST.STONE_TYPES = B.BLOCKS_STONE_TYPE
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

        var machinesByMapId = new Dictionary<string, List<DumpRecipeMapMachine>>();
        foreach (var r in db.Query<(string MapId, string ItemId, long Multiblock, int? Tier, long Steam)>("""
            SELECT GREG_TECH_RECIPE_MAP_ID, MACHINES_ITEM_ID, MACHINES_MULTIBLOCK, MACHINES_TIER, MACHINES_STEAM
            FROM GREG_TECH_RECIPE_MAP_MACHINES
            """))
        {
            Add(machinesByMapId, r.MapId,
                new DumpRecipeMapMachine(r.ItemId, r.Multiblock != 0, r.Tier, r.Steam != 0));
        }

        var recipeMaps = new Dictionary<string, DumpRecipeMap>();
        foreach (var r in db.Query<(string Id, string Unlocalized, string Name, int Amperage, long Single, long Multi, long Fuel)>("""
            SELECT ID, UNLOCALIZED_NAME, LOCALIZED_NAME, AMPERAGE, HAS_SINGLE_BLOCK, HAS_MULTI_BLOCK, IS_FUEL
            FROM GREG_TECH_RECIPE_MAP
            """))
        {
            recipeMaps[r.Unlocalized] = new DumpRecipeMap(
                r.Unlocalized, r.Name, r.Amperage, r.Single != 0, r.Multi != 0, r.Fuel != 0,
                machinesByMapId.GetValueOrDefault(r.Id) ?? []);
        }

        // A GregTech recipe type is named rt~gregtech~<recipe map>~<voltage>.
        var recipeMapByTypeId = new Dictionary<string, DumpRecipeMap>();
        foreach (var typeId in db.Query<string>("""SELECT ID FROM RECIPE_TYPE WHERE CATEGORY = 'gregtech'"""))
        {
            var parts = typeId.Split('~');
            if (parts.Length == 4 && recipeMaps.TryGetValue(parts[2], out var map))
            {
                recipeMapByTypeId[typeId] = map;
            }
        }

        // The exporter already folds map amperage into each recipe row; a divergence here means
        // that convention changed and power math would silently break.
        var amperageDivergences = 0;
        foreach (var recipe in recipes)
        {
            if (recipeMapByTypeId.TryGetValue(recipe.RecipeTypeId, out var map)
                && map.Amperage > 1
                && gt.TryGetValue(recipe.Id, out var data)
                && data.Amperage != map.Amperage)
            {
                amperageDivergences++;
            }
        }
        if (amperageDivergences > 0)
        {
            logger.LogWarning(
                "{Count} recipes diverge from their map's amperage; recipe-level amps kept",
                amperageDivergences);
        }

        var blockDrops = new List<DumpBlockDrop>();
        foreach (var r in db.Query<(string Id, string BlockItemId, string DropItemId, int Quantity)>("""
            SELECT ID, BLOCK_ITEM_ID, DROP_ID, QUANTITY
            FROM BLOCK_DROP
            WHERE BLOCK_ITEM_ID IS NOT NULL AND DROP_ID IS NOT NULL AND QUANTITY > 0
            """))
        {
            blockDrops.Add(new DumpBlockDrop(r.Id, r.BlockItemId, r.DropItemId, r.Quantity));
        }

        var cropDrops = new Dictionary<string, List<string>>();
        foreach (var (cropId, itemId) in db.Query<(string, string)>(
            """SELECT CROPS_NH_CROP_ID, DROPS_ITEM_ID FROM CROPS_NH_CROP_DROPS"""))
        {
            Add(cropDrops, cropId, itemId);
        }
        var cropUnderBlocks = new Dictionary<string, List<string>>();
        foreach (var (cropId, itemId) in db.Query<(string, string)>(
            """SELECT CROPS_NH_CROP_ID, UNDER_BLOCKS_ITEM_ID FROM CROPS_NH_CROP_UNDER_BLOCKS"""))
        {
            Add(cropUnderBlocks, cropId, itemId);
        }
        var crops = new List<DumpCrop>();
        foreach (var r in db.Query<(string Id, string CropId, string Name, string? SeedId, long Hidden)>("""
            SELECT ID, CROP_ID, NAME, SEED_ID, HIDDEN FROM CROPS_NH_CROP
            """))
        {
            crops.Add(new DumpCrop(
                r.Id, r.CropId, r.Name, r.SeedId, r.Hidden != 0,
                cropDrops.GetValueOrDefault(r.Id) ?? [],
                cropUnderBlocks.GetValueOrDefault(r.Id) ?? []));
        }

        var undergroundFluids = new List<DumpUndergroundFluid>();
        foreach (var r in db.Query<(string FluidId, string Dimension, int Tier)>("""
            SELECT F.FLUID_ID, D.ABBREVIATION, D.ROCKET_TIER
            FROM GREG_TECH_UNDERGROUND_FLUID F
            JOIN GREG_TECH_UNDERGROUND_FLUID_DIMENSIONS FD ON FD.GREG_TECH_UNDERGROUND_FLUID_ID = F.ID
            JOIN GREG_TECH_DIMENSION D ON D.ABBREVIATION = FD.DIMENSIONS_DIMENSION_ABBREVIATION
            WHERE F.FLUID_ID IS NOT NULL
            """))
        {
            undergroundFluids.Add(new DumpUndergroundFluid(r.FluidId, r.Dimension, r.Tier));
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
            UnifiedOredictTargets = unifiedOredictTargets,
            UnificationBlacklist = unificationBlacklist,
            OrePrefixes = new OrePrefixIndex(orePrefixes),
            ItemContainers = itemContainers,
            ItemData = new ItemDataIndex(itemData),
            ItemInputsByRecipe = itemInputs,
            ItemOutputsByRecipe = itemOutputs,
            FluidInputsByRecipe = fluidInputs,
            FluidOutputsByRecipe = fluidOutputs,
            ContainersByItemId = containers,
            HandlerItemsByRecipeTypeId = handlerItems,
            WorldgenOres = worldgenOres,
            RecipeMapByTypeId = recipeMapByTypeId,
            BlockDrops = blockDrops,
            Crops = crops,
            UndergroundFluids = undergroundFluids,
            MachineVoltageTiers = machineVoltageTiers,
            Generators = ReadGenerators(db),
            Dynamos = ReadDynamos(db),
            Boilers = ReadBoilers(db),
            MultiblockMachines = ReadMultiblockMachines(db),
            TurbineRotors = ReadTurbineRotors(db),
            MobDropItemIds = ReadMobDropItemIds(db),
            DeprecatedItems = ReadDeprecatedItems(db),
            ExporterVersion = metadata.Version ?? "unknown",
            ExportedAt = DateTimeOffset.FromUnixTimeMilliseconds(metadata.CreatedMillis)
        };
    }

    private static List<DumpGenerator> ReadGenerators(SqliteConnection db)
    {
        RequireMachineProps(db, "GREG_TECH_GENERATOR");
        // The efficiency column mixes INTEGER and REAL rows; CAST keeps Dapper's row shape stable.
        return [.. db.Query<(string ItemId, double Efficiency, long MaxEuOutput, long AmpsOut)>("""
            SELECT ITEM_ID, CAST(EFFICIENCY AS REAL), MAX_EU_OUTPUT, AMPERES_OUT
            FROM GREG_TECH_GENERATOR
            """).Select(r => new DumpGenerator(r.ItemId, r.Efficiency, r.MaxEuOutput, r.AmpsOut))];
    }

    private static List<DumpDynamo> ReadDynamos(SqliteConnection db)
    {
        RequireMachineProps(db, "GREG_TECH_DYNAMO");
        return [.. db.Query<(string ItemId, long MaxEuOutput, long AmpsOut)>("""
            SELECT ITEM_ID, MAX_EU_OUTPUT, AMPERES_OUT FROM GREG_TECH_DYNAMO
            """).Select(r => new DumpDynamo(r.ItemId, r.MaxEuOutput, r.AmpsOut))];
    }

    private static List<DumpBoiler> ReadBoilers(SqliteConnection db)
    {
        RequireMachineProps(db, "GREG_TECH_LARGE_BOILER");
        return [.. db.Query<(string ItemId, long EuT)>("""
            SELECT ITEM_ID, EUT FROM GREG_TECH_LARGE_BOILER
            """).Select(r => new DumpBoiler(r.ItemId, (int)r.EuT))];
    }

    private static List<DumpMultiblockMachine> ReadMultiblockMachines(SqliteConnection db)
    {
        RequireMachineProps(db, "GREG_TECH_MULTIBLOCK_MACHINE");
        var bonuses = new Dictionary<string, List<DumpMultiblockBonus>>();
        foreach (var r in db.Query<(string Id, string Kind, double Value, long Multiplicative, string? TierAxis)>("""
            SELECT GREG_TECH_MULTIBLOCK_MACHINE_ID, BONUSES_KIND, BONUSES_BONUS_VALUE,
                BONUSES_MULTIPLICATIVE, BONUSES_TIER_AXIS
            FROM GREG_TECH_MULTIBLOCK_MACHINE_BONUSES
            """))
        {
            Add(bonuses, r.Id, new DumpMultiblockBonus(
                r.Kind, r.Value, r.Multiplicative != 0, r.TierAxis));
        }

        return [.. db.Query<(string Id, string ItemId, long? MaxParallel)>("""
            SELECT ID, ITEM_ID, MAX_PARALLEL_RECIPES FROM GREG_TECH_MULTIBLOCK_MACHINE
            """).Select(r => new DumpMultiblockMachine(
            r.ItemId, (int?)r.MaxParallel, bonuses.GetValueOrDefault(r.Id) ?? []))];
    }

    private static List<DumpTurbineRotor> ReadTurbineRotors(SqliteConnection db)
    {
        RequireMachineProps(db, "GREG_TECH_TURBINE_ROTOR");
        var stats = new Dictionary<string, List<DumpRotorFuelStats>>();
        foreach (var r in db.Query<(string Id, string Fuel, double Efficiency, double LooseEfficiency, double OptimalFlow, double LooseOptimalFlow, double OptimalEut, double LooseOptimalEut)>("""
            SELECT GREG_TECH_TURBINE_ROTOR_ID, FUEL_STATS_FUEL, FUEL_STATS_EFFICIENCY,
                FUEL_STATS_LOOSE_EFFICIENCY, FUEL_STATS_OPTIMAL_FLOW,
                FUEL_STATS_LOOSE_OPTIMAL_FLOW, FUEL_STATS_OPTIMAL_EUT,
                FUEL_STATS_LOOSE_OPTIMAL_EUT
            FROM GREG_TECH_TURBINE_ROTOR_FUEL_STATS
            """))
        {
            Add(stats, r.Id, new DumpRotorFuelStats(
                r.Fuel, r.Efficiency, r.LooseEfficiency, r.OptimalFlow, r.LooseOptimalFlow,
                r.OptimalEut, r.LooseOptimalEut));
        }

        return [.. db.Query<(string Id, string ItemId, string Size, string Material, long Durability, double BaseEfficiency, long Overflow)>("""
            SELECT ID, ITEM_ID, SIZE, MATERIAL_NAME, MAX_DURABILITY, BASE_EFFICIENCY,
                OVERFLOW_EFFICIENCY
            FROM GREG_TECH_TURBINE_ROTOR
            """).Select(r => new DumpTurbineRotor(
            r.ItemId, r.Size, r.Material, r.Durability, r.BaseEfficiency, (int)r.Overflow,
            stats.GetValueOrDefault(r.Id) ?? []))];
    }

    /// <summary>Items marked with GT's deprecation banner — a rigid tooltip line, not prose:
    /// every machine-block match in the 2.9.0 dump is the same "superseded controller" wave.</summary>
    private static HashSet<string> ReadDeprecatedItems(SqliteConnection db)
    {
        var deprecated = new HashSet<string>();
        foreach (var (itemId, tooltip) in db.Query<(string, string)>(
            """SELECT ITEM_ID, TOOLTIP FROM ITEM_TOOLTIP WHERE TOOLTIP LIKE '%DEPRECATED%'"""))
        {
            foreach (var line in tooltip.Split('\n'))
            {
                var text = line.TrimStart();
                if (text.StartsWith("\u00a74DEPRECATED", StringComparison.Ordinal)
                    || text.StartsWith("\u00a74[DEPRECATED", StringComparison.Ordinal))
                {
                    deprecated.Add(itemId);
                    break;
                }
            }
        }
        return deprecated;
    }

    private HashSet<string> ReadMobDropItemIds(SqliteConnection db)
    {
        if (!HasTable(db, "MOB_INFO_DROPS"))
        {
            logger.LogWarning("dump has no mob drops; no mob-farm seeds will ship");
            return [];
        }
        return [.. db.Query<string>("""
            SELECT DISTINCT d.DROPS_ITEM_ID
            FROM MOB_INFO_DROPS d
            JOIN MOB_INFO m ON m.ID = d.MOB_INFO_ID
            WHERE m.SOUL_VIAL_USABLE = 1 AND d.DROPS_ITEM_ID IS NOT NULL
            """)];
    }

    private static void RequireMachineProps(SqliteConnection db, string table)
    {
        if (!HasTable(db, table))
        {
            throw new InvalidOperationException(
                $"dump predates {table}; re-export with exporter 0.6.5 or later");
        }
    }

    private static bool HasTable(SqliteConnection db, string table) =>
        db.Query<string>("SELECT name FROM sqlite_master WHERE type = 'table'")
            .Any(name => name.Equals(table, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lets the builder read a dump taken before a column existed.</summary>
    private static bool HasColumn(SqliteConnection db, string table, string column) =>
        db.Query<string>($"SELECT name FROM pragma_table_info('{table}')")
            .Any(name => name.Equals(column, StringComparison.OrdinalIgnoreCase));

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }
        list.Add(value);
    }
}
