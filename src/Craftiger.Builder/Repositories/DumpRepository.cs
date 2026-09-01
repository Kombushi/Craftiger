using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Models.Dump;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Craftiger.Builder.Repositories;

public sealed class DumpRepository(
    IDumpItemReader items,
    IDumpOredictReader oredict,
    IDumpRecipeReader recipes,
    IDumpWorldgenReader worldgen,
    IDumpMachineReader machines,
    IDumpCropReader crops,
    ILogger<DumpRepository> logger) : IDumpRepository
{
    public Dump Read(string dumpPath)
    {
        using var db = new SqliteConnection($"Data Source={dumpPath};Mode=ReadOnly");
        db.Open();

        var itemSet = items.Read(db);
        var oredictSet = oredict.Read(db);
        var recipeSet = recipes.Read(db);
        var worldgenSet = worldgen.Read(db);
        var machineSet = machines.Read(db);
        var cropSet = crops.Read(db);
        var metadata = ReadMetadata(db);
        WarnOnAmperageDivergence(recipeSet, machineSet);

        return new Dump
        {
            Items = itemSet.Items,
            Fluids = itemSet.Fluids,
            Recipes = recipeSet.Recipes,
            GtByRecipeId = recipeSet.GtByRecipeId,
            GroupStacks = recipeSet.GroupStacks,
            Oredict = oredictSet.Oredict,
            UnifiedOredictTargets = oredictSet.UnifiedOredictTargets,
            UnificationBlacklist = oredictSet.UnificationBlacklist,
            OrePrefixes = oredictSet.OrePrefixes,
            ItemContainers = oredictSet.ItemContainers,
            ItemData = oredictSet.ItemData,
            ItemInputsByRecipe = recipeSet.ItemInputsByRecipe,
            ItemOutputsByRecipe = recipeSet.ItemOutputsByRecipe,
            FluidInputsByRecipe = recipeSet.FluidInputsByRecipe,
            FluidOutputsByRecipe = recipeSet.FluidOutputsByRecipe,
            ContainersByItemId = recipeSet.ContainersByItemId,
            HandlerItemsByRecipeTypeId = recipeSet.HandlerItemsByRecipeTypeId,
            WorldgenOres = worldgenSet.WorldgenOres,
            RecipeMapByTypeId = machineSet.RecipeMapByTypeId,
            Machines = machineSet.Machines,
            BlockDrops = cropSet.BlockDrops,
            Crops = cropSet.Crops,
            UndergroundFluids = worldgenSet.UndergroundFluids,
            MachineVoltageTiers = itemSet.MachineVoltageTiers,
            Generators = machineSet.Generators,
            Dynamos = machineSet.Dynamos,
            Boilers = machineSet.Boilers,
            MultiblockMachines = machineSet.MultiblockMachines,
            TurbineRotors = machineSet.TurbineRotors,
            TreeFarmTools = machineSet.TreeFarmTools,
            Coils = machineSet.Coils,
            Engines = machineSet.Engines,
            ReactorModes = machineSet.ReactorModes,
            Constants = machineSet.Constants,
            Mobs = cropSet.Mobs,
            MobDropsByMob = cropSet.MobDropsByMob,
            Fertilizers = cropSet.Fertilizers,
            FluidFertilizers = cropSet.FluidFertilizers,
            FarmComponents = cropSet.FarmComponents,
            DeprecatedItems = itemSet.DeprecatedItems,
            ExporterVersion = metadata.ExporterVersion,
            ExportedAt = metadata.ExportedAt,
        };
    }

    private static DumpMetadata ReadMetadata(SqliteConnection db)
    {
        var metadata = db.Query<(string Version, long CreatedMillis)>(
            """SELECT VERSION, CREATION_TIME_MILLIS FROM METADATA LIMIT 1""").FirstOrDefault();
        return new DumpMetadata(
            metadata.Version ?? "unknown",
            DateTimeOffset.FromUnixTimeMilliseconds(metadata.CreatedMillis));
    }

    /// <summary>The exporter folds map amperage into each recipe row; a divergence means that convention changed.</summary>
    private void WarnOnAmperageDivergence(DumpRecipeSet recipeSet, DumpMachineSet machineSet)
    {
        var amperageDivergences = 0;
        foreach (var recipe in recipeSet.Recipes)
        {
            if (machineSet.RecipeMapByTypeId.TryGetValue(recipe.RecipeTypeId, out var map)
                && map.Amperage > 1
                && recipeSet.GtByRecipeId.TryGetValue(recipe.Id, out var data)
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
    }
}
