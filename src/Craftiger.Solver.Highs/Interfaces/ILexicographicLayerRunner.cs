using System.Diagnostics;
using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Models.Lp;
using Highs;

namespace Craftiger.Solver.Highs.Interfaces;

/// <summary>Runs a program's layers as sequential solves on one loaded instance, each optimum becoming a lock row for the next.</summary>
public interface ILexicographicLayerRunner
{
    LayerOutcome Run(HighsLpSolver solver, LinearProgram program, LpScaling scaling, Stopwatch? deadline);
}
