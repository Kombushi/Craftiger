using Craftiger.Builder.Interfaces;
using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Options;
using Craftiger.Builder.Models.Planner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Craftiger.Builder.Services;

/// <summary>One run is one kill on diamond spikes with no weapon: base drops at looting zero.</summary>
public sealed class MobLineRecipeService(
    IOptions<FarmsConfiguration> options,
    ILogger<MobLineRecipeService> logger) : IMobLineRecipeService
{
    private const long BaseEuT = 1920;

    private const double SpikeDamage = 9.0;

    private const long SpawnIntervalTicks = 55;

    private readonly FarmsConfiguration _config = options.Value;

    public MobLines Run(Dump dump, UnifiedItems unified)
    {
        var eecId = unified.Canonical(_config.EecItemId);
        if (!dump.Items.ContainsKey(eecId))
        {
            logger.LogWarning("entity crusher {ItemId} is unknown to this dump; no mob lines ship", eecId);
            return new MobLines([], []);
        }
        var machine = new RecipeMachine(eecId, Multiblock: true, Tier: null, Steam: false);
        var machines = new List<PlannerMachineItem>
        {
            new(_config.EecMap, eecId, Tier: null, Multiblock: true, Steam: false, Era: null),
        };
        var spawnerId = unified.Canonical(_config.SpawnerItemId);
        var xpJuice = dump.IsFluid(_config.XpJuiceFluidId) ? _config.XpJuiceFluidId : null;

        var recipes = new List<PlannerRecipe>();
        foreach (var mob in dump.Mobs)
        {
            if (!mob.SoulVialUsable)
            {
                continue;
            }
            var outputs = new List<PlannerOutput>();
            foreach (var group in mob.Drops
                .Where(drop => drop.Type != "INFERNAL" && dump.Items.ContainsKey(drop.ItemId))
                .GroupBy(drop => unified.Canonical(drop.ItemId)))
            {
                var expected = group.Sum(drop => drop.Probability * drop.StackSize);
                if (expected <= 0)
                {
                    continue;
                }
                var amount = (long)Math.Ceiling(expected);
                outputs.Add(new PlannerOutput(group.Key, amount, expected / amount));
            }
            if (outputs.Count == 0)
            {
                continue;
            }
            if (xpJuice is not null)
            {
                outputs.Add(new PlannerOutput(xpJuice, _config.XpJuicePerKill, 1.0));
            }

            var euT = mob.AlwaysInfernal ? BaseEuT * 8 : BaseEuT;
            recipes.Add(new PlannerRecipe(
                $"eec~{mob.MobId}", _config.EecMap, TierLadder.VoltageTier(euT), Heat: null,
                Math.Max(SpawnIntervalTicks, (long)(mob.Health / SpikeDamage * 10)), euT, Amps: 1,
                new Dictionary<string, long>(), [], outputs, [machine], [],
                RequiresCleanroom: false, RequiresLowGravity: false)
            {
                Catalysts = [new PlannerCatalystSlot([new PlannerCatalyst(spawnerId, 1, Tool: false)])],
                Overclock = OverclockMode.EntityCrusher,
                Scope = RecipeScope.FactoryMob,
            });
        }

        logger.LogInformation("  {Rows:N0} mob lines", recipes.Count);
        return new MobLines(recipes, machines);
    }
}
