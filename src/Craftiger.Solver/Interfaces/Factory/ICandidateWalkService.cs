using Craftiger.Solver.Models.Factory;

namespace Craftiger.Solver.Interfaces.Factory;

/// <summary>The candidate recipe set: the consume targets' downstream cone plus the cost-banded upstream closure of the produce targets, fuels and co-inputs.</summary>
public interface ICandidateWalkService
{
    CandidateSet Walk(
        FactoryContext context, IEnumerable<int> targets, IEnumerable<int> consumed, IReadOnlyDictionary<string, string> pins,
        bool mobFarms);
}
