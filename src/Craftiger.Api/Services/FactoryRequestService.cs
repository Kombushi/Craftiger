using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Craftiger.Api.Interfaces;
using Craftiger.Api.Models;
using Craftiger.Solver.Models.Factory;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.Services;

/// <summary>Shape-level validation into the solver's request; an unknown target or pin item is left to the solve, which answers it as a structured warning rather than a 400.</summary>
public sealed class FactoryRequestService(
    PlannerArtifact artifact,
    IOptions<ApiOptions> options) : IFactoryRequestService
{
    public FactoryRequest Translate(FactorySolveRequest request)
    {
        if (request.Targets is not { Count: > 0 })
        {
            throw new ValidationException("a factory solve needs at least one target");
        }

        var maxTier = artifact.TierNames.Count - 1;
        var targets = new List<FactoryTarget>(request.Targets.Count);
        foreach (var target in request.Targets)
        {
            var kind = KindOf(target.Kind);
            if (!double.IsFinite(target.Rate) || target.Rate <= 0)
            {
                throw new ValidationException($"the rate of a '{target.Kind}' target must be positive");
            }
            if (kind != FactoryTargetKind.Energy && string.IsNullOrEmpty(target.ItemId))
            {
                throw new ValidationException($"a '{target.Kind}' target must name an item");
            }
            if (target.GeneratorTier is { } tier && (tier < 0 || tier > maxTier))
            {
                throw new ValidationException($"the generator tier must be between 0 and {maxTier}, not {tier}");
            }
            targets.Add(new FactoryTarget(
                kind, kind == FactoryTargetKind.Energy ? null : target.ItemId, target.Rate, target.GeneratorTier));
        }

        List<FactoryStep>? steps = null;
        if (request.Steps is { Count: > 0 })
        {
            steps = new List<FactoryStep>(request.Steps.Count);
            foreach (var step in request.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    throw new ValidationException("a pipeline step must name a recipe or generator line");
                }
                if (step.OcSteps is < 0)
                {
                    throw new ValidationException($"the overclock of step '{step.Id}' cannot be negative");
                }
                steps.Add(new FactoryStep(step.Id, step.MachineItemId, step.OcSteps));
            }
        }

        List<string>? supplies = null;
        if (request.Supplies is { Count: > 0 })
        {
            if (request.Supplies.Any(string.IsNullOrWhiteSpace))
            {
                throw new ValidationException("a supply must name an item");
            }
            supplies = request.Supplies;
        }

        return new FactoryRequest(
            targets,
            (request.Priority ?? []).Select(ObjectiveOf).ToList(),
            request.Pins ?? new Dictionary<string, string>(),
            request.MobFarms,
            request.BredSeeds,
            options.Value.FactoryTimeLimitSeconds,
            steps,
            supplies);
    }

    /// <summary>Everything that shapes the plan hashes into the id — pins and the scope toggles included; priority keeps its order because the layers run in it. A pipeline hashes its steps and supplies in place of the pins the solve ignores.</summary>
    public string FactoryIdOf(FactorySolveRequest request)
    {
        var canonical = CacheKeys.Settings(new SolveRequest(request.Garage, request.B, request.Weights));
        CacheKeys.Append(canonical, "targets", (request.Targets ?? []).Select(target =>
            $"{target.Kind?.ToLowerInvariant()}:{target.ItemId}:" +
            $"{target.Rate.ToString("R", CultureInfo.InvariantCulture)}:{target.GeneratorTier?.ToString() ?? "any"}"));
        canonical.Append(";priority=").AppendJoin(',', (request.Priority ?? []).Select(name => name.ToLowerInvariant()));
        if (request.Steps is { Count: > 0 } || request.Supplies is { Count: > 0 })
        {
            CacheKeys.Append(canonical, "steps", (request.Steps ?? []).Select(step =>
                $"{step.Id}@{step.MachineItemId ?? "auto"}@{step.OcSteps?.ToString() ?? "auto"}"));
            CacheKeys.Append(canonical, "supplies", (request.Supplies ?? []).Order(StringComparer.Ordinal));
        }
        else
        {
            CacheKeys.Append(canonical, "pins", (request.Pins ?? []).Select(pin => $"{pin.Key}={pin.Value}"));
        }
        canonical.Append(";mob=").Append(request.MobFarms).Append(";bred=").Append(request.BredSeeds);
        return CacheKeys.Hash(canonical);
    }

    private static FactoryTargetKind KindOf(string? kind) => kind?.ToLowerInvariant() switch
    {
        "produce" => FactoryTargetKind.Produce,
        "consume" => FactoryTargetKind.Consume,
        "energy" => FactoryTargetKind.Energy,
        var other => throw new ValidationException($"unknown target kind '{other}'"),
    };

    private static FactoryObjective ObjectiveOf(string? name) => name?.ToLowerInvariant() switch
    {
        "resource" => FactoryObjective.Resource,
        "energy" => FactoryObjective.Energy,
        "machines" => FactoryObjective.Machines,
        var other => throw new ValidationException($"unknown objective '{other}'"),
    };
}
