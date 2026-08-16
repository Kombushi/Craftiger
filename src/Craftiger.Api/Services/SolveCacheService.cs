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
            var entry = new SolveEntry(table, garage, weights, Sort(table), CountReachable(table));
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

    private int CountReachable(CostTable table) =>
        artifact.Items.Keys.Count(table.Costs.ContainsKey);

    private IReadOnlyList<SortedRow> Sort(CostTable table)
    {
        var rows = artifact.Items.Values
            .Select(item => new SortedRow(
                item.Id,
                table.Costs.TryGetValue(item.Id, out var cost) ? cost : null))
            .ToList();
        rows.Sort((a, b) =>
        {
            if (a.Cost is null != b.Cost is null)
            {
                return a.Cost is null ? 1 : -1;
            }
            var byCost = (a.Cost ?? 0).CompareTo(b.Cost ?? 0);
            return byCost != 0
                ? byCost
                : string.CompareOrdinal(artifact.Items[a.ItemId].Name, artifact.Items[b.ItemId].Name);
        });
        return rows;
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

        return (
            new Garage(
                request.Garage.DefaultTier,
                request.Garage.Machines ?? [],
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
