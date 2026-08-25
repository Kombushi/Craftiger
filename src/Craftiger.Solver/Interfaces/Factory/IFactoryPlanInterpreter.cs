using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Reads an optimal solution back as lines, flows and inflows.</summary>
public interface IFactoryPlanInterpreter
{
    FactoryPlan Interpret(
        FactoryContext context,
        FactoryModel model,
        FactoryTargets targets,
        IReadOnlyList<double> values,
        IReadOnlyList<FactoryWarning> warnings,
        AutoInfiniteItems infinite);
}
