using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Craftiger.Builder.Services;

/// <summary>The single composition root shared by the console entry point and the tests.</summary>
public static class BuilderServiceCollectionExtensions
{
    public static IServiceCollection AddBuilderServices(this IServiceCollection services) =>
        services
            .AddLogging(builder => builder
                .AddConsoleFormatter<BuilderConsoleFormatter, ConsoleFormatterOptions>()
                .AddConsole(options => options.FormatterName = BuilderConsoleFormatter.FormatterName))
            .AddSingleton(BuilderConfig.Default)
            .AddSingleton<IDumpRepository, DumpRepository>()
            .AddSingleton<IPlannerRepository, PlannerRepository>()
            .AddSingleton<IUnificationService, UnificationService>()
            .AddSingleton<IRecipeTransformService, RecipeTransformService>()
            .AddSingleton<ILeafTaggingService, LeafTaggingService>()
            .AddSingleton<IBlockBreakRecipeService, BlockBreakRecipeService>()
            .AddSingleton<IUndergroundFluidRecipeService, UndergroundFluidRecipeService>()
            .AddSingleton<IWorldgenErasService, WorldgenErasService>()
            .AddSingleton<IEraSolveService, EraSolveService>()
            .AddSingleton<IAtlasBuilder, AtlasBuilder>()
            .AddSingleton<IBuilderPipeline, BuilderPipeline>();
}
