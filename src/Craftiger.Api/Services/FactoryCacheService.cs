using System.Collections.Concurrent;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Models.Factory;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.Services;

/// <summary>The factory cache in the solve cache's two tiers: single-flight per factoryId in front of the shared store; the cost solve behind a plan runs through its own cache, so only a fresh plan pays for it.</summary>
public sealed class FactoryCacheService(
    PlannerArtifact artifact,
    IFactoryRequestService requests,
    ISolveCacheService costs,
    IFactorySolverService solver,
    ISolveStore store,
    IFactoryPlanCodec codec,
    IOptions<ApiOptions> options,
    ILogger<FactoryCacheService> logger) : IFactoryCacheService
{
    private const string StorePrefix = "factory:";

    private readonly ConcurrentDictionary<string, FactoryCacheSlot> _entries = new();
    private readonly int _capacity = Math.Max(1, options.Value.FactoryCacheSize);
    private long _clock;

    public async Task<FactoryResponse> SolveAsync(FactorySolveRequest request)
    {
        var translated = requests.Translate(request);
        var factoryId = requests.FactoryIdOf(request);
        var slot = _entries.GetOrAdd(factoryId, id => new FactoryCacheSlot(new Lazy<Task<FactoryPlan>>(
            () => FetchOrComputeAsync(id, request, translated), LazyThreadSafetyMode.ExecutionAndPublication)));
        slot.LastUsed = Interlocked.Increment(ref _clock);
        FactoryPlan plan;
        try
        {
            plan = await slot.Plan.Value;
        }
        catch
        {
            _entries.TryRemove(new KeyValuePair<string, FactoryCacheSlot>(factoryId, slot));
            throw;
        }
        // A transient outcome is answered but never kept, so the next request starts over.
        if (plan.Status is FactoryPlanStatus.TimedOut or FactoryPlanStatus.Failed)
        {
            _entries.TryRemove(new KeyValuePair<string, FactoryCacheSlot>(factoryId, slot));
        }
        Evict();
        return Respond(factoryId, plan);
    }

    private async Task<FactoryPlan> FetchOrComputeAsync(
        string factoryId, FactorySolveRequest request, FactoryRequest translated)
    {
        if (await store.GetAsync(StorePrefix + factoryId) is { } payload)
        {
            if (codec.Decode(payload) is { } stored)
            {
                return stored;
            }
            logger.LogWarning("factory {FactoryId}: stored plan is not this artifact's or is unreadable; recomputing", factoryId);
        }
        var solve = await costs.SolveAsync(new SolveRequest(request.Garage, request.B, request.Weights));
        var entry = await costs.GetAsync(solve.SolveId)
            ?? throw new InvalidOperationException($"cost solve {solve.SolveId} vanished before the factory solve read it");
        var context = new FactoryContext(
            artifact.Graph,
            artifact.Factory.Recipes, artifact.Factory.Machines, artifact.Factory.Seeds,
            artifact.Factory.Steam, artifact.Factory.Environment,
            entry.Table, entry.Garage, entry.Weights);
        var plan = solver.Solve(context, translated);
        logger.LogInformation(
            "factory {FactoryId}: {Status} with {Lines:N0} lines, {Warnings:N0} warnings",
            factoryId, plan.Status, plan.Lines.Count, plan.Warnings.Count);
        if (plan.Status is not (FactoryPlanStatus.TimedOut or FactoryPlanStatus.Failed))
        {
            await StoreAsync(factoryId, plan);
        }
        return plan;
    }

    /// <summary>The response waits for the write so a follow-up on another replica finds the plan; a failed write is logged and only costs a later recompute.</summary>
    private async Task StoreAsync(string factoryId, FactoryPlan plan)
    {
        try
        {
            await store.PutAsync(StorePrefix + factoryId, codec.Encode(plan));
        }
        catch (Exception e)
        {
            logger.LogError(e, "factory {FactoryId} could not be written to the store", factoryId);
        }
    }

    /// <summary>Drops the least recently used settled plans until the cache fits; in-flight work is never evicted, and a race may drop one entry too many, costing a fetch.</summary>
    private void Evict()
    {
        while (_entries.Count > _capacity)
        {
            KeyValuePair<string, FactoryCacheSlot>? stalest = null;
            foreach (var pair in _entries)
            {
                if (pair.Value.IsSettled
                    && (stalest is null || pair.Value.LastUsed < stalest.Value.Value.LastUsed))
                {
                    stalest = pair;
                }
            }
            if (stalest is null || !_entries.TryRemove(stalest.Value))
            {
                return;
            }
        }
    }

    /// <summary>The plan plus the display lookup for every item it names; costs stay off — an inflow row already carries its charged weight.</summary>
    private FactoryResponse Respond(string factoryId, FactoryPlan plan)
    {
        var ids = new HashSet<string>();
        foreach (var line in plan.Lines)
        {
            if (line.MachineItemId is { } machineItem)
            {
                ids.Add(machineItem);
            }
        }
        ids.UnionWith(plan.Flows.Select(flow => flow.ItemId));
        ids.UnionWith(plan.Inflows.Select(inflow => inflow.ItemId));
        ids.UnionWith(plan.Warnings.Where(warning => warning.ItemId.Length > 0).Select(warning => warning.ItemId));
        var items = ids.Where(artifact.Items.ContainsKey).ToDictionary(
            id => id,
            id =>
            {
                var item = artifact.Items[id];
                return new ItemRefDto(
                    item.Name, item.AtlasIdx, item.IsFluid, item.LeafClass, null, item.Uncraftable, item.MaxStack);
            });
        return new FactoryResponse(
            factoryId, plan.Status, plan.Lines, plan.Flows, plan.Inflows, plan.Warnings,
            plan.PricedInflowCost, plan.DrawEuT, plan.ExportEuT, plan.BusyMachines, items);
    }
}
