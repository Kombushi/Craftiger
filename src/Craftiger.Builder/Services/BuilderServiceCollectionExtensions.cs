using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Repositories;
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
            .AddSingleton<IDumpRepository, DumpRepository>()
            .AddSingleton<IPlannerRepository, PlannerRepository>()
            .AddSingleton<IUnificationService, UnificationService>()
            .AddSingleton<IRecipeTransformService, RecipeTransformService>()
            .AddSingleton<IConservationService, ConservationService>()
            .AddSingleton<ILeafTaggingService, LeafTaggingService>()
            .AddSingleton<IBlockBreakRecipeService, BlockBreakRecipeService>()
            .AddSingleton<IUndergroundFluidRecipeService, UndergroundFluidRecipeService>()
            .AddSingleton<ICropHarvestRecipeService, CropHarvestRecipeService>()
            .AddSingleton<IWorldgenErasService, WorldgenErasService>()
            .AddSingleton<IFuelExtractionService, FuelExtractionService>()
            .AddSingleton<IMachinePropsService, MachinePropsService>()
            .AddSingleton<IRenewableSeedsService, RenewableSeedsService>()
            .AddSingleton<IEraSolveService, EraSolveService>()
            .AddSingleton<IPriceCheckService, PriceCheckService>()
            .AddSingleton<IAtlasBuilder, AtlasBuilder>()
            .AddSingleton<IBuilderPipeline, BuilderPipeline>();
}
