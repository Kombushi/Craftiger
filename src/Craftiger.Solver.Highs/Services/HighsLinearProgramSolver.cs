using System.Diagnostics;
using Craftiger.Solver.Highs.Interfaces;
using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Interfaces.Lp;
using Craftiger.Solver.Models.Lp;
using Highs;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Highs.Services;

/// <summary>HiGHS-backed lexicographic LP on one private native instance per call: the library's thread safety is undocumented, so instances are never shared.</summary>
public sealed class HighsLinearProgramSolver(
    IHighsModelLoader loader,
    ILexicographicLayerRunner layers,
    IOptions<HighsOptions> options) : ILinearProgramSolver
{
    private readonly HighsOptions _options = options.Value;

    public LinearProgramResult Solve(LinearProgram program)
    {
        if (program.Objectives.Count == 0)
        {
            throw new ArgumentException("A linear program needs at least one objective.", nameof(program));
        }

        using var solver = new HighsLpSolver();
        loader.Configure(solver);
        var deadline = program.TimeLimitSeconds > 0 ? Stopwatch.StartNew() : null;
        var scaling = LpScaling.Equilibrate(program, _options.EquilibrationPasses);
        loader.Load(solver, program, scaling);

        var outcome = layers.Run(solver, program, scaling, deadline);
        return outcome.Standing is { } standing
            ? new LinearProgramResult(LpSolveStatus.Optimal, scaling.Unscale(standing))
            : LinearProgramResult.Failed(outcome.Status);
    }
}
