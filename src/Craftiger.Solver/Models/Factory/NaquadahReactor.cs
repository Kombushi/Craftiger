namespace Craftiger.Solver.Models.Factory;

/// <summary>A mode-driven reactor: timed fuels at their fixed EU/t, output multiplied by an optional coolant, output and fuel together by an optional excited liquid, over a flat upkeep drain.</summary>
public sealed record NaquadahReactor(
    IReadOnlyList<GeneratorMode> Upkeeps,
    IReadOnlyList<GeneratorMode> Coolants,
    IReadOnlyList<GeneratorMode> ExcitedLiquids)
{
    public static NaquadahReactor? Of(IReadOnlyList<GeneratorMode> modes)
    {
        var upkeeps = modes.Where(mode => mode.Kind == GeneratorModeKind.Upkeep).ToList();
        var coolants = modes.Where(mode => mode.Kind == GeneratorModeKind.Coolant).ToList();
        var excited = modes.Where(mode => mode.Kind == GeneratorModeKind.Excited).ToList();
        return upkeeps.Count + coolants.Count + excited.Count == 0
            ? null
            : new NaquadahReactor(upkeeps, coolants, excited);
    }

    /// <summary>Every coolant-excited combination on a timed fuel, the bare run included.</summary>
    public IEnumerable<ReactorRun> Runs(FactoryFuel fuel)
    {
        if (fuel.EuT is not { } euT || fuel.DurationTicks is not { } duration || duration <= 0)
        {
            yield break;
        }
        foreach (var coolant in Coolants.Cast<GeneratorMode?>().Prepend(null))
        {
            foreach (var excited in ExcitedLiquids.Cast<GeneratorMode?>().Prepend(null))
            {
                var times = excited?.Factor ?? 1;
                var consumes = new List<(string FluidId, double PerSecond)>();
                foreach (var upkeep in Upkeeps)
                {
                    consumes.Add((upkeep.FluidId, upkeep.PerSecond));
                }
                if (coolant is not null)
                {
                    consumes.Add((coolant.FluidId, coolant.PerSecond));
                }
                if (excited is not null)
                {
                    consumes.Add((excited.FluidId, excited.PerSecond));
                }
                yield return new ReactorRun(
                    fuel.Amount * times * Ticks.PerSecond / duration,
                    euT * (coolant?.Factor ?? 1) * times,
                    fuel.ReturnAmount * times * Ticks.PerSecond / duration,
                    consumes,
                    Variant(coolant, excited));
            }
        }
    }

    private static string? Variant(GeneratorMode? coolant, GeneratorMode? excited)
    {
        var parts = new List<string>();
        if (coolant is not null)
        {
            parts.Add($"c~{coolant.FluidId}");
        }
        if (excited is not null)
        {
            parts.Add($"x~{excited.FluidId}");
        }
        return parts.Count == 0 ? null : string.Join('|', parts);
    }
}
