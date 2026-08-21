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

public sealed class SolveCacheService(
    PlannerArtifact artifact, ICostSolverService solver, IOptions<ApiOptions> options,
    ILogger<SolveCacheService> logger) : ISolveCacheService
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SolveEntry> _entries = [];
    private readonly LinkedList<string> _recency = [];
    private readonly int _capacity = Math.Max(1, options.Value.SolveCacheSize);

    /// <summary>Every item id in craft-list tie order (name, then id), fixed per artifact: a
    /// solve only has to order the priced items by cost and keep this order inside each price.</summary>
    private readonly string[] _byName = artifact.Items.Values
        .OrderBy(item => item.Name, StringComparer.Ordinal)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .Select(item => item.Id)
        .ToArray();

    public SolveResponse Solve(SolveRequest request)
    {
        var (garage, weights) = Translate(request);
        var solveId = SolveIdOf(request);

        lock (_gate)
        {
            if (_entries.TryGetValue(solveId, out var cached))
            {
                Touch(solveId);
                return new SolveResponse(solveId, cached.ReachableCount, cached.Table.Converged);
            }

            var table = solver.Solve(artifact.Graph, garage, weights);
            var (sorted, reachable) = Sort(table);
            var entry = new SolveEntry(table, garage, weights, sorted, reachable);
            _entries[solveId] = entry;
            _recency.AddFirst(solveId);
            while (_entries.Count > _capacity)
            {
                var evicted = _recency.Last!.Value;
                _recency.RemoveLast();
                _entries.Remove(evicted);
            }

            logger.LogInformation(
                "solved {SolveId}: {Reachable:N0} of {Items:N0} items priced, converged {Converged}",
                solveId, entry.ReachableCount, artifact.Items.Count, table.Converged);
            return new SolveResponse(solveId, entry.ReachableCount, table.Converged);
        }
    }

    public SolveEntry? Get(string solveId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(solveId, out var entry))
            {
                return null;
            }
            Touch(solveId);
            return entry;
        }
    }

    private void Touch(string solveId)
    {
        _recency.Remove(solveId);
        _recency.AddFirst(solveId);
    }

    /// <summary>The craft list: priced items cheapest first, unreachable items after them, ties
    /// in the fixed name order. Only the priced items are sorted, by cost and then by their
    /// position in that order, so no comparison touches a string.</summary>
    private (IReadOnlyList<string> Sorted, int ReachableCount) Sort(CostTable table)
    {
        var costs = new double[_byName.Length];
        var isPriced = new bool[_byName.Length];
        var priced = new List<int>(table.Costs.Count);
        for (var rank = 0; rank < _byName.Length; rank++)
        {
            if (table.Costs.TryGetValue(_byName[rank], out var cost))
            {
                costs[rank] = cost;
                isPriced[rank] = true;
                priced.Add(rank);
            }
        }
        var order = priced.ToArray();
        Array.Sort(order, (a, b) => costs[a] != costs[b] ? costs[a].CompareTo(costs[b]) : a.CompareTo(b));

        var sorted = new string[_byName.Length];
        var next = 0;
        foreach (var rank in order)
        {
            sorted[next++] = _byName[rank];
        }
        for (var rank = 0; rank < _byName.Length; rank++)
        {
            if (!isPriced[rank])
            {
                sorted[next++] = _byName[rank];
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
