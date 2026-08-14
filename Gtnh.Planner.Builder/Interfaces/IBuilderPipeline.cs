using Gtnh.Planner.Builder.Models;

namespace Gtnh.Planner.Builder.Interfaces;

/// <summary>Runs the dump-to-artifacts pipeline end to end.</summary>
public interface IBuilderPipeline
{
    int Run(BuilderOptions options);
}
