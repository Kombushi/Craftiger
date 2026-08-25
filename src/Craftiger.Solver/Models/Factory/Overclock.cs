namespace Craftiger.Solver.Models.Factory;

/// <summary>An overclock of so many steps, the first Perfect of them energy-neutral: a standard step halves duration for quadrupled power, a perfect one quarters duration for the same.</summary>
public readonly record struct Overclock(int Steps, int Perfect)
{
    public int Standard => Steps - Perfect;

    public double DurationDivisor => Math.Pow(2, Standard) * Math.Pow(4, Perfect);

    /// <summary>Energy per run after the duration shrinks: perfect steps are free, standard ones double it.</summary>
    public double EuMultiplier => Math.Pow(2, Standard);

    /// <summary>Power draw per tick, quadrupled every step whatever the step does with the time.</summary>
    public double PowerMultiplier => Math.Pow(4, Steps);

    /// <summary>Every overclock from none to the voltage gap, perfect steps first; a recipe drawing nothing has no power to trade and runs at base speed only.</summary>
    public static IEnumerable<Overclock> Ladder(int maxSteps, int perfectSteps, bool drawsPower)
    {
        var top = drawsPower ? Math.Max(0, maxSteps) : 0;
        for (var k = 0; k <= top; k++)
        {
            yield return new Overclock(k, Math.Min(k, perfectSteps));
        }
    }
}
