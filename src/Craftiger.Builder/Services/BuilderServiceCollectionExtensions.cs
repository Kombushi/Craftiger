using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Interfaces.DumpReaders;
using Craftiger.Builder.Interfaces.Eras;
using Craftiger.Builder.Interfaces.Recipes;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Repositories;
using Craftiger.Builder.Repositories.DumpReaders;
using Craftiger.Builder.Services.Eras;
using Craftiger.Builder.Services.Recipes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Craftiger.Builder.Services;

public static class BuilderServiceCollectionExtensions
{
    public static IServiceCollection AddBuilderServices(
        this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddLogging(builder => builder
                .ClearProviders()
                .AddConfiguration(configuration.GetSection("Logging"))
                .AddConsoleFormatter<BuilderConsoleFormatter, ConsoleFormatterOptions>()
                .AddConsole(options => options.FormatterName = BuilderConsoleFormatter.FormatterName))
            .Configure<BuilderOptions>(configuration.GetSection(nameof(BuilderOptions)))
            .Configure<RecipesConfiguration>(configuration.GetSection(nameof(RecipesConfiguration)))
            .Configure<ErasConfiguration>(configuration.GetSection(nameof(ErasConfiguration)))
            .Configure<WorldConfiguration>(configuration.GetSection(nameof(WorldConfiguration)))
            .Configure<SynthesizedMachinesConfiguration>(
                configuration.GetSection(nameof(SynthesizedMachinesConfiguration)))
            .Configure<PricingConfiguration>(configuration.GetSection(nameof(PricingConfiguration)))
            .Configure<FuelsConfiguration>(configuration.GetSection(nameof(FuelsConfiguration)))
            .Configure<RenewableSeedsConfiguration>(
                configuration.GetSection(nameof(RenewableSeedsConfiguration)))
            .Configure<MachineOverlayConfiguration>(
                configuration.GetSection(nameof(MachineOverlayConfiguration)))
            .Configure<SteamConfiguration>(configuration.GetSection(nameof(SteamConfiguration)))
            .Configure<TreeFarmConfiguration>(configuration.GetSection(nameof(TreeFarmConfiguration)))
            .AddSingleton<IDumpItemReader, DumpItemReader>()
            .AddSingleton<IDumpOredictReader, DumpOredictReader>()
            .AddSingleton<IDumpRecipeReader, DumpRecipeReader>()
            .AddSingleton<IDumpWorldgenReader, DumpWorldgenReader>()
            .AddSingleton<IDumpMachineReader, DumpMachineReader>()
            .AddSingleton<IDumpCropReader, DumpCropReader>()
            .AddSingleton<IDumpRepository, DumpRepository>()
            .AddSingleton<IPlannerRepository, PlannerRepository>()
            .AddSingleton<IUnificationService, UnificationService>()
            .AddSingleton<ISteamSynthesisService, SteamSynthesisService>()
            .AddSingleton<IRecipeMachineListService, RecipeMachineListService>()
            .AddSingleton<IRecipeSlotResolver, RecipeSlotResolver>()
            .AddSingleton<IRecipeVariantService, RecipeVariantService>()
            .AddSingleton<ICraftingGridService, CraftingGridService>()
            .AddSingleton<IRecipeTransformService, RecipeTransformService>()
            .AddSingleton<ITreeFarmRecipeService, TreeFarmRecipeService>()
            .AddSingleton<IConservationService, ConservationService>()
            .AddSingleton<ILeafTaggingService, LeafTaggingService>()
            .AddSingleton<IBlockBreakRecipeService, BlockBreakRecipeService>()
            .AddSingleton<IUndergroundFluidRecipeService, UndergroundFluidRecipeService>()
            .AddSingleton<ICropHarvestRecipeService, CropHarvestRecipeService>()
            .AddSingleton<IWorldgenErasService, WorldgenErasService>()
            .AddSingleton<IFuelExtractionService, FuelExtractionService>()
            .AddSingleton<IMachinePropsService, MachinePropsService>()
            .AddSingleton<IRenewableSeedsService, RenewableSeedsService>()
            .AddSingleton<IEraSeedService, EraSeedService>()
            .AddSingleton<IEraPropagationService, EraPropagationService>()
            .AddSingleton<ILeafTierService, LeafTierService>()
            .AddSingleton<IMachineAvailabilityService, MachineAvailabilityService>()
            .AddSingleton<IEraSolveService, EraSolveService>()
            .AddSingleton<IPriceCheckService, PriceCheckService>()
            .AddSingleton<IAtlasBuilder, AtlasBuilder>()
            .AddSingleton<IBuilderPipeline, BuilderPipeline>();
}
