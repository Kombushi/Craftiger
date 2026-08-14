using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

/// <summary>Writes planner.sqlite from the transformed model.</summary>
public interface IPlannerRepository
{
    void Write(string path, PlannerData data);
}
