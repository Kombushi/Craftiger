namespace Craftiger.Builder.Interfaces;

/// <summary>Runs the dump-to-artifacts pipeline end to end.</summary>
public interface IBuilderPipeline
{
    int Run();
}