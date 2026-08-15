using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Craftiger.Builder.Services;

/// <summary>The single composition root shared by the console entry point and the tests.</summary>
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
            .Configure<BuilderConfig>(configuration.GetSection(nameof(BuilderConfig)))
            .AddSingleton<IDumpRepository, DumpRepository>()
            .AddSingleton<IPlannerRepository, PlannerRepository>()
            .AddSingleton<IUnificationService, UnificationService>()
            .AddSingleton<IRecipeTransformService, RecipeTransformService>()
            .AddSingleton<ILeafTaggingService, LeafTaggingService>()
            .AddSingleton<IBlockBreakRecipeService, BlockBreakRecipeService>()
            .AddSingleton<IUndergroundFluidRecipeService, UndergroundFluidRecipeService>()
            .AddSingleton<ICropHarvestRecipeService, CropHarvestRecipeService>()
            .AddSingleton<IWorldgenErasService, WorldgenErasService>()
            .AddSingleton<IEraSolveService, EraSolveService>()
            .AddSingleton<IPriceCheckService, PriceCheckService>()
            .AddSingleton<IAtlasBuilder, AtlasBuilder>()
            .AddSingleton<IBuilderPipeline, BuilderPipeline>();
}