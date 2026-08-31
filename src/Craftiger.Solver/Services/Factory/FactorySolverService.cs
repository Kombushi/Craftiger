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

        var generators = targets.HasEnergy ? generatorCatalog.Eligible(context, targets.Bands) : [];
        if (targets.HasEnergy && generators.Count == 0
            || targets.Bands.Any(band => !generators.Any(line => line.Satisfies(band))))
        {
            warnings.Add(FactoryWarning.NoGenerator());
            return FactoryPlan.Empty(FactoryPlanStatus.Infeasible, warnings);
        }

        // Steam is drawn by machine variants, not recipe inputs, so the walk would never reach its producers on its own.
        var steamItems = context.Machines.HasBuildableSteamBlock(context.Garage) ? context.SteamItems() : [];
        var walkTargets = targets.Produce.Keys
            .Concat(generators.Select(line => line.FuelItem))
            .Concat(generators.SelectMany(line => line.Inputs.Select(flow => flow.Item)))
            .Concat(steamItems)
            .Distinct();
        var candidates = candidateWalk.Walk(context, walkTargets, targets.Consume.Keys, request.Pins, request.MobFarms);
        if (candidates.Pruned)
        {
            warnings.Add(FactoryWarning.RoutesPruned());
        }
        var unreachable = false;
        foreach (var target in targets.ProducedItems)
        {
            if (!index.IsLeaf(target) && !candidates.Candidates.Any(recipe => index.Produces(recipe, target)))
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

        var infinite = autoInfinite.Reach(context, seedItems, request.MobFarms);
        return interpreter.Interpret(context, model, targets, result.ColumnValues, warnings, infinite);
    }
}
