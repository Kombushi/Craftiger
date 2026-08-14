using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Writes planner.sqlite from the transformed model.</summary>
public interface IPlannerRepository
{
    void Write(string path, PlannerData data);
}
