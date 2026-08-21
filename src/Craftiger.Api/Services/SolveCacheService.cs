using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.Services;

/// <summary>The solve cache in two tiers. In this process, entries live in a concurrent map
/// whose values are lazily started tasks, so a solve runs exactly once per id while every
/// concurrent request for that id awaits the same work and nothing else waits at all;
/// recency is a tick per entry and eviction drops the stalest settled one, so no lock is held
/// anywhere. Behind it, the store keeps every solved entry for any replica and across
/// restarts: an id this process does not hold is fetched before it is computed, and a fresh
/// solve is written to the store before the response leaves, so the request that follows
/// may land on any replica.</summary>
public sealed class SolveCacheService(
    PlannerArtifact artifact,
    ICostSolverService solver,
    ISolveStore store,
    ISolveEntryCodec codec,
    IOptions<ApiOptions> options,
    ILogger<SolveCacheService> logger) : ISolveCacheService
{
    private readonly ConcurrentDictionary<string, SolveCacheSlot> _entries = new();
    private readonly int _capacity = Math.Max(1, options.Value.SolveCacheSize);
    private long _clock;

    /// <summary>Per craft-list rank, the item's position in the solver index, or -1 for an item
    /// no recipe and no leaf class ever mention — it can only ever be unpriced.</summary>
    private readonly int[] _positionOfRank = artifact.CraftListOrder
        .Select(id => artifact.Graph.Index.TryGetItem(id, out var item) ? item : -1)
        .ToArray();

    public async Task<SolveResponse> SolveAsync(SolveRequest request)
    {
        var (garage, weights) = Translate(request);
        var solveId = SolveIdOf(request);
        var slot = _entries.GetOrAdd(solveId, id => new SolveCacheSlot(new Lazy<Task<SolveEntry>>(
            () => FetchOrComputeAsync(id, garage, weights), LazyThreadSafetyMode.ExecutionAndPublication)));
        var entry = await SettleAsync(solveId, slot);
        Evict();
        return new SolveResponse(solveId, entry.ReachableCount, entry.Table.Converged);
    }

    public async Task<SolveEntry?> GetAsync(string solveId)
    {
        if (_entries.TryGetValue(solveId, out var slot))
        {
            return await SettleAsync(solveId, slot);
        }
        // Read-through: another replica may have solved it, or this process may have restarted.
        var fetched = await FetchAsync(solveId);
        if (fetched is null)
        {
            return null;
        }
        var adopted = _entries.GetOrAdd(solveId, _ => new SolveCacheSlot(new Lazy<Task<SolveEntry>>(Task.FromResult(fetched))));
        var entry = await SettleAsync(solveId, adopted);
        Evict();
        return entry;
    }

    /// <summary>The slot's entry, awaiting work in flight; a slot whose work threw is dropped so
    /// the next request starts over instead of replaying the failure, and the error surfaces.</summary>
    private async Task<SolveEntry> SettleAsync(string solveId, SolveCacheSlot slot)
    {
        slot.LastUsed = Interlocked.Increment(ref _clock);
        try
        {
            return await slot.Entry.Value;
        }
        catch
        {
            _entries.TryRemove(new KeyValuePair<string, SolveCacheSlot>(solveId, slot));
            throw;
        }
    }

    private async Task<SolveEntry> FetchOrComputeAsync(string solveId, Garage garage, WeightSettings weights)
    {
        var stored = await FetchAsync(solveId);
        if (stored is not null)
        {
            return stored;
        }
        var entry = Compute(solveId, garage, weights);
        await StoreAsync(solveId, entry);
        return entry;
    }

    private async Task<SolveEntry?> FetchAsync(string solveId)
    {
        var payload = await store.GetAsync(solveId);
        if (payload is null)
        {
            return null;
        }
        var entry = codec.Decode(payload);
        if (entry is null)
        {
            logger.LogWarning("solve {SolveId}: stored entry is not this artifact's or is unreadable; recomputing", solveId);
        }
        return entry;
    }

    /// <summary>The response waits for the write — a few milliseconds on a solve of a second —
    /// so a follow-up request on another replica finds the entry; a failed write is logged and
    /// only costs a later process a recompute, the solve itself still stands.</summary>
    private async Task StoreAsync(string solveId, SolveEntry entry)
    {
        try
        {
            await store.PutAsync(solveId, codec.Encode(entry));
        }
        catch (Exception e)
        {
            logger.LogError(e, "solve {SolveId} could not be written to the store", solveId);
        }
    }

    private SolveEntry Compute(string solveId, Garage garage, WeightSettings weights)
    {
        var table = solver.Solve(artifact.Graph, garage, weights);
        var (sorted, reachable) = Sort(table);
        logger.LogInformation(
            "solved {SolveId}: {Reachable:N0} of {Items:N0} items priced, converged {Converged}",
            solveId, reachable, artifact.Items.Count, table.Converged);
        return new SolveEntry(table, garage, weights, sorted, reachable);
    }

    /// <summary>Drops the least recently used settled entries until the cache fits; work still
    /// in flight is never evicted. Two requests evicting at once may drop one entry more than
    /// needed, which costs a fetch and nothing else.</summary>
    private void Evict()
    {
        while (_entries.Count > _capacity)
        {
            KeyValuePair<string, SolveCacheSlot>? stalest = null;
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

    /// <summary>The craft list: priced items cheapest first, unreachable items after them, ties
    /// in the artifact's fixed name order. Only the priced ranks are sorted, by cost and then
    /// by rank, so no comparison touches a string.</summary>
    private (IReadOnlyList<int> Sorted, int ReachableCount) Sort(CostTable table)
    {
        var ranks = _positionOfRank.Length;
        var costs = new double[ranks];
        var priced = new List<int>(table.PricedCount);
        for (var rank = 0; rank < ranks; rank++)
        {
            var position = _positionOfRank[rank];
            if (position >= 0 && table.TryCost(position, out var cost))
            {
                costs[rank] = cost;
                priced.Add(rank);
            }
            else
            {
                costs[rank] = double.NaN;
            }
        }
        var order = priced.ToArray();
        Array.Sort(order, (a, b) =>
        {
            var byCost = costs[a].CompareTo(costs[b]);
            return byCost != 0 ? byCost : a.CompareTo(b);
        });

        var sorted = new int[ranks];
        order.CopyTo(sorted, 0);
        var next = order.Length;
        for (var rank = 0; rank < ranks; rank++)
        {
            if (double.IsNaN(costs[rank]))
            {
                sorted[next++] = rank;
            }
        }
        return (sorted, order.Length);
    }

    private (Garage Garage, WeightSettings Weights) Translate(SolveRequest request)
    {
        if (!double.IsFinite(request.B) || request.B <= 0)
        {
            throw new ValidationException($"b must be a positive number, not {request.B}");
        }

        var maxTier = artifact.TierNames.Count - 1;
        ValidateTier(request.Garage.DefaultTier, "the default tier", maxTier);
        foreach (var (machine, tier) in request.Garage.Machines ?? [])
        {
            if (tier is { } owned)
            {
                ValidateTier(owned, $"the tier of '{machine}'", maxTier);
            }
        }

        var coilHeat = new Dictionary<string, int>();
        foreach (var (machine, coilName) in request.Garage.Coils ?? [])
        {
            var coil = artifact.Coils.FirstOrDefault(c => c.Name == coilName)
                ?? throw new ValidationException($"unknown coil '{coilName}' on '{machine}'");
            coilHeat[machine] = coil.MaxHeat;
        }

        var weights = request.Weights ?? [];
        foreach (var (itemId, weight) in weights)
        {
            if (!double.IsFinite(weight) || weight < 0)
            {
                throw new ValidationException($"the weight of '{itemId}' must be a non-negative number");
            }
        }

        // The default only covers machines whose block is craftable by then (§2): a recipe
        // being LV says nothing about when its machine can be built. Explicit entries win,
        // and a machine whose era the model never resolved stays lenient rather than
        // turning a reachability gap into a pricing hole.
        var machines = new Dictionary<string, int?>(request.Garage.Machines ?? []);
        foreach (var machine in artifact.Machines)
        {
            if (!machine.AlwaysOwned
                && !machines.ContainsKey(machine.Name)
                && machine.Era is { } era
                && era > request.Garage.DefaultTier)
            {
                machines[machine.Name] = null;
            }
        }

        return (
            new Garage(
                request.Garage.DefaultTier,
                machines,
                (request.Garage.BuiltMultiblocks ?? []).ToHashSet(),
                coilHeat),
            new WeightSettings(request.B, weights));
    }

    private static void ValidateTier(int tier, string what, int maxTier)
    {
        if (tier < 0 || tier > maxTier)
        {
            throw new ValidationException($"{what} must be between 0 and {maxTier}, not {tier}");
        }
    }

    /// <summary>A stable content hash, so identical settings land on the same cache entry.</summary>
    private static string SolveIdOf(SolveRequest request)
    {
        var canonical = new StringBuilder();
        canonical.Append("b=").Append(request.B.ToString("R", CultureInfo.InvariantCulture));
        canonical.Append(";default=").Append(request.Garage.DefaultTier);
        Append(canonical, "machines", (request.Garage.Machines ?? [])
            .Select(m => $"{m.Key}={m.Value?.ToString() ?? "none"}"));
        Append(canonical, "built", request.Garage.BuiltMultiblocks ?? []);
        Append(canonical, "coils", (request.Garage.Coils ?? []).Select(c => $"{c.Key}={c.Value}"));
        Append(canonical, "weights", (request.Weights ?? [])
            .Select(w => $"{w.Key}={w.Value.ToString("R", CultureInfo.InvariantCulture)}"));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..32];
    }

    private static void Append(StringBuilder canonical, string label, IEnumerable<string> parts)
    {
        canonical.Append(';').Append(label).Append('=');
        canonical.AppendJoin(',', parts.Order(StringComparer.Ordinal));
    }
}
