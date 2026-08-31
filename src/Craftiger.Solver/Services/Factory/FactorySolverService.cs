using Craftiger.Solver.Interfaces.Costs;
using Craftiger.Solver.Interfaces.Factory;
using Craftiger.Solver.Interfaces.Lp;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Lp;

namespace Craftiger.Solver.Services.Factory;

public sealed class FactorySolverService(
    IFactoryTargetService targetService,
    IGeneratorCatalogService generatorCatalog,
    ICandidateWalkService candidateWalk,
    IFactoryModelService modelService,
    IAutoInfiniteService autoInfinite,
    IFactoryDiagnosisService diagnosis,
    IFactoryPlanInterpreter interpreter,
    IGarageLegalityService legality,
    ILinearProgramSolver solver) : IFactorySolverService
{
    public FactoryPlan Solve(FactoryContext context, FactoryRequest request)
    {
        var index = context.Index;
        var warnings = new List<FactoryWarning>();
        var targets = targetService.Normalize(index, request, warnings);
        if (targets is null)
        {
            return FactoryPlan.Empty(FactoryPlanStatus.Failed, warnings);
        }

        CandidateSet candidates;
        IReadOnlyList<GeneratorLine> generators;
        if (request.IsPipeline)
        {
            (candidates, var lineIds) = ResolveSteps(context, request, warnings);
            generators = targets.HasEnergy
                ? generatorCatalog.Eligible(context, targets.Bands, prune: false)
                    .Where(line => lineIds.Contains(line.LineId(index)))
                    .ToList()
                : [];
            foreach (var id in lineIds.Except(generators.Select(line => line.LineId(index))).Order())
            {
                warnings.Add(FactoryWarning.StepUnknown(id));
            }
        }
        else
        {
            generators = targets.HasEnergy ? generatorCatalog.Eligible(context, targets.Bands) : [];
            // Steam is drawn by machine variants, not recipe inputs, so the walk would never reach its producers on its own.
            var steamItems = context.Machines.HasBuildableSteamBlock(context.Garage) ? context.SteamItems() : [];
            var walkTargets = targets.Produce.Keys
                .Concat(generators.Select(line => line.FuelItem))
                .Concat(generators.SelectMany(line => line.Inputs.Select(flow => flow.Item)))
                .Concat(steamItems)
                .Distinct();
            candidates = candidateWalk.Walk(context, walkTargets, targets.Consume.Keys, request);
            if (candidates.Pruned)
            {
                warnings.Add(FactoryWarning.RoutesPruned());
            }
        }
        if (targets.HasEnergy && generators.Count == 0
            || targets.Bands.Any(band => !generators.Any(line => line.Satisfies(band))))
        {
            warnings.Add(FactoryWarning.NoGenerator());
            return FactoryPlan.Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        var unreachable = false;
        foreach (var target in targets.ProducedItems)
        {
            // A pipeline never buys its produce targets, so only a step counts as making one.
            var made = request.IsPipeline
                ? candidates.Candidates.Any(recipe => index.Produces(recipe, target))
                    || generators.Any(line =>
                        line.CondensateItem == target || line.Outputs.Any(flow => flow.Item == target))
                : index.IsLeaf(target) || candidates.Candidates.Any(recipe => index.Produces(recipe, target));
            if (!made)
            {
                warnings.Add(FactoryWarning.UnreachableTarget(index.ItemIds[target]));
                unreachable = true;
            }
        }
        if (unreachable)
        {
            return FactoryPlan.Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        var seedItems = context.Seeds.Resolve(index, request.MobFarms);
        var model = modelService.Build(context, request, targets, candidates, generators, seedItems, warnings);
        var result = solver.Solve(model.Program);
        if (result.Status == LpSolveStatus.Infeasible)
        {
            diagnosis.Diagnose(context, model, warnings);
            return FactoryPlan.Empty(FactoryPlanStatus.Infeasible, warnings);
        }
        if (result.Status != LpSolveStatus.Optimal)
        {
            var (status, warning) = result.Status switch
            {
                LpSolveStatus.Unbounded => (FactoryPlanStatus.Unbounded, FactoryWarning.FreeLunch()),
                LpSolveStatus.TimedOut => (FactoryPlanStatus.TimedOut, FactoryWarning.Timeout()),
                _ => (FactoryPlanStatus.Failed, FactoryWarning.SolverError()),
            };
            warnings.Add(warning);
            return FactoryPlan.Empty(status, warnings);
        }

        var infinite = autoInfinite.Reach(context, seedItems, request);
        return interpreter.Interpret(context, model, targets, result.ColumnValues, warnings, infinite);
    }

    /// <summary>The steps' recipes as the whole candidate set: no walk, no pruning, no scope gate — an explicit step is its own consent — but never past garage or environment legality; ids naming no recipe are handed back as generator line candidates.</summary>
    private (CandidateSet Candidates, IReadOnlySet<string> LineIds) ResolveSteps(
        FactoryContext context, FactoryRequest request, List<FactoryWarning> warnings)
    {
        var index = context.Index;
        var recipes = new HashSet<int>();
        var lineIds = new HashSet<string>();
        foreach (var step in request.Steps!)
        {
            if (!index.TryGetRecipe(step.Id, out var recipe))
            {
                lineIds.Add(step.Id);
                continue;
            }
            if (!legality.IsLegal(index, recipe, context.Garage)
                || !context.Environment.Admits(context.Recipes, recipe, context.Garage))
            {
                warnings.Add(FactoryWarning.StepIllegal(step.Id));
                continue;
            }
            recipes.Add(recipe);
        }
        return (new CandidateSet([.. recipes.Order()], new HashSet<int>(), Pruned: false), lineIds);
    }
}
