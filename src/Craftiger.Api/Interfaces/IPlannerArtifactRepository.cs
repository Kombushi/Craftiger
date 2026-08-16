using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>Loads planner.sqlite into memory once, refusing unknown schema versions.</summary>
public interface IPlannerArtifactRepository
{
    PlannerArtifact Load(string artifactsDir);
}
