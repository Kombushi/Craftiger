using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>Every way the garage can run a recipe: blocks, overclocks and steam.</summary>
public interface IRunVariantService
{
    IReadOnlyList<RunVariant> Variants(FactoryContext context, int recipe);
}
