using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models;
using Craftiger.Builder.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Craftiger.Builder.Services;

/// <summary>The single composition root shared by the console entry point and the tests.</summary>
public static class BuilderServiceCollectionExtensions
{
    public static IServiceCollection AddBuilderServices(this IServiceCollection services) =>
        services
            .AddSingleton(BuilderConfig.Default)
            .AddSingleton<IDumpRepository, DumpRepository>()
            .AddSingleton<IPlannerRepository, PlannerRepository>()
            .AddSingleton<IUnificationService, UnificationService>()
            .AddSingleton<IRecipeTransformService, RecipeTransformService>()
            .AddSingleton<ILeafTaggingService, LeafTaggingService>()
            .AddSingleton<IOreWorldgenService, OreWorldgenService>()
            .AddSingleton<IIngotTiersService, IngotTiersService>()
            .AddSingleton<IAtlasBuilder, AtlasBuilder>()
            .AddSingleton<IBuilderPipeline, BuilderPipeline>();
}
