using Craftiger.Solver.Models;

namespace Craftiger.Solver.UnitTests;

/// <summary>LP model construction against the recording fake; the solved-plan behavior runs
/// end-to-end in the HiGHS test project.</summary>
public class PipelineSolverTests
{
    [Fact]
    public void BuildsBalanceRowsWithTargetBound()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("ingot", 2)]));

        var program = lp.Program!;
        Assert.Equal(2, program.Rows.Count);
        Assert.Equal(2, program.Rows[0].Lower);
        Assert.Equal(double.PositiveInfinity, program.Rows[0].Upper);
        Assert.Equal(0, program.Rows[1].Lower);
        Assert.Equal(2, program.Columns.Count);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, -1)], program.Columns[0].Entries);
        Assert.Equal([new LpEntry(1, 1)], program.Columns[1].Entries);
    }

    [Fact]
    public void SplitsChoiceSlotsThroughLinkRows()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("x"), Fx.Leaf("y")],
            Fx.Recipe("mix", slots: [[("x", 2), ("y", 3)]], outputs: ("out", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("out", 1)]));

        var program = lp.Program!;
        // Rows: target, link; then x and y through the split columns. Columns: run, two
        // splits, two buys.
        var link = program.Rows[1];
        Assert.Equal(0, link.Lower);
        Assert.Equal(0, link.Upper);
        Assert.Equal(5, program.Columns.Count);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, -1)], program.Columns[0].Entries);
        Assert.Equal([new LpEntry(1, 1), new LpEntry(2, -2)], program.Columns[1].Entries);
        Assert.Equal([new LpEntry(1, 1), new LpEntry(3, -3)], program.Columns[2].Entries);
    }

    [Fact]
    public void PinZerosOtherDeterministicProducers()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("a"), Fx.Leaf("b"), Fx.Leaf("c")],
            Fx.Recipe("route1", inputs: [("a", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("route2", inputs: [("b", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("chanced", inputs: [("c", 1)], outputs: ("plate", 1, 0.5)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(),
            Fx.Request([("plate", 1)], pins: new() { ["plate"] = "route2" }));

        var program = lp.Program!;
        Assert.Equal(0, program.Columns[0].Upper);
        Assert.Equal(double.PositiveInfinity, program.Columns[1].Upper);
        Assert.Equal(double.PositiveInfinity, program.Columns[2].Upper);
    }

    [Fact]
    public void UnknownTargetFailsWithoutSolving()
    {
        var graph = Fx.Graph([Fx.Leaf("ore")]);
        var lp = new RecordingLpSolver();

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("nope", 1)]));

        Assert.Equal(FactoryPlanStatus.Failed, plan.Status);
        Assert.Contains(new FactoryWarning("target_unknown", "nope"), plan.Warnings);
        Assert.Null(lp.Program);
    }

    [Fact]
    public void UnreachableTargetReportsBeforeSolving()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", machine: "Electric Blast Furnace", tier: 5, inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph, Fx.Garage(defaultTier: 1)), Fx.Garage(defaultTier: 1), Fx.Weights(), Fx.Request([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("unreachable_target", "ingot"), plan.Warnings);
        Assert.Null(lp.Program);
    }

    [Fact]
    public void ObjectivesFollowPriorityWithCanonicalizationLast()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", weight: 7)],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (100, 30, 2) }), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 1)], priority: [FactoryObjective.Machines, FactoryObjective.Resource]));

        var program = lp.Program!;
        Assert.Equal(3, program.Objectives.Count);
        Assert.Equal([new LpEntry(0, 5)], program.Objectives[0].Coefficients);
        Assert.Equal([new LpEntry(1, 7)], program.Objectives[1].Coefficients);
        Assert.Equal([new LpEntry(0, 1), new LpEntry(1, 1)], program.Objectives[2].Coefficients);
    }

    [Fact]
    public void EnergyLayerPricesDutyCycledDraw()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (100, 30, 2) }), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 1)]));

        // kEU per run: 100 ticks × 30 EU/t × 2 A ÷ 1000.
        Assert.Equal([new LpEntry(0, 6)], lp.Program!.Objectives[1].Coefficients);
    }

    [Fact]
    public void InterpretsSolutionIntoPlan()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore", weight: 3)],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver
        {
            Result = new LinearProgramResult(LpSolveStatus.Optimal, [2.0, 2.0]),
        };

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (40, 8, 1) }), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(),
            Fx.Request([("ingot", 2)]));

        Assert.Equal(FactoryPlanStatus.Solved, plan.Status);
        var line = Assert.Single(plan.Lines);
        Assert.Equal("smelt", line.RecipeId);
        Assert.Equal(2, line.RunsPerSecond);
        Assert.Equal(4, line.BusyMachines);
        Assert.False(line.Durationless);
        Assert.Null(line.MachineItemId);
        Assert.Equal(0, line.OcSteps);
        Assert.Equal(1, line.Parallels);
        Assert.True(line.Estimated);
        Assert.Contains(plan.Flows, f => f is { ItemId: "ingot", Produced: 2, Surplus: 0 });
        Assert.Contains(plan.Flows, f => f is { ItemId: "ore", Consumed: 2 });
        var inflow = Assert.Single(plan.Inflows);
        Assert.Equal(("ore", 2.0, 3.0), (inflow.ItemId, inflow.Rate, inflow.Weight));
        Assert.Equal(6, plan.PricedInflowCost);
        Assert.Equal(32, plan.DrawEuT);
        Assert.Equal(4, plan.BusyMachines);
    }

    [Fact]
    public void SolverFailureSurfacesAsWarning()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var lp = new RecordingLpSolver
        {
            Result = new LinearProgramResult(LpSolveStatus.Infeasible, []),
        };

        var plan = Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), FactoryMachineData.Empty, Fx.Costs(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("ingot", 1)]));

        Assert.Equal(FactoryPlanStatus.Infeasible, plan.Status);
        Assert.Contains(new FactoryWarning("infeasible", ""), plan.Warnings);
    }

    [Fact]
    public void ExpandsMachineAndOverclockVariants()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("smelt", machine: "Mac", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var machines = Fx.Machines(new()
        {
            ["Mac"] = [Fx.Block("b-lv", tier: 1), Fx.Block("b-mv", tier: 2)],
        });
        var garage = Fx.Garage(defaultTier: 2);
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (80, 8, 1) }), machines,
            Fx.Costs(graph, garage), garage, Fx.Weights(), Fx.Request([("ingot", 1)]));

        // Five run columns: the LV block at k = 0..1, the MV block at k = 0..2, then the buy.
        var program = lp.Program!;
        Assert.Equal(6, program.Columns.Count);
        var energy = program.Objectives[1].Coefficients;
        Assert.Equal([0.64, 1.28, 0.64, 1.28, 2.56], energy.Select(e => e.Value));
        var machinesLayer = program.Objectives[2].Coefficients;
        Assert.Equal([4.0, 2.0, 4.0, 2.0, 1.0], machinesLayer.Select(e => e.Value));
    }

    [Fact]
    public void ResolvesVolcanusShapeBonuses()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("blast", machine: "Volcano", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var machines = Fx.Machines(new()
        {
            ["Volcano"] =
            [
                Fx.Block("volc", multiblock: true, maxParallel: 8,
                    bonuses:
                    [
                        new FactoryMachineBonus("SPEED", 220, false, null),
                        new FactoryMachineBonus("EU_DISCOUNT", 90, false, null),
                    ]),
            ],
        });
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["blast"] = (100, 10, 1) }), machines,
            Fx.Costs(graph), Fx.Garage(), Fx.Weights(), Fx.Request([("ingot", 1)]));

        // 220 % speed and 90 % EU: per run 100 t x 10 EU/t x 0.9 x 100/220; busy divided by 8.
        var program = lp.Program!;
        Assert.Equal(0.40909, Assert.Single(program.Objectives[1].Coefficients).Value, 1e-4);
        Assert.Equal(0.28409, Assert.Single(program.Objectives[2].Coefficients).Value, 1e-4);
    }

    [Fact]
    public void HeatExcessTurnsOverclocksPerfect()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore")],
            Fx.Recipe("blast", machine: "Hot", heat: 1800, inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var machines = Fx.Machines(new()
        {
            ["Hot"] = [Fx.Block("hot-mv", tier: 2)],
        });
        var garage = Fx.Garage(defaultTier: 2, coils: new() { ["Hot"] = 3600 });
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["blast"] = (100, 10, 1) }), machines,
            Fx.Costs(graph, garage), garage, Fx.Weights(), Fx.Request([("ingot", 1)]));

        // 1800 excess heat: one perfect step and a 0.95^2 EU discount. The first overclock is
        // energy-neutral and quarters the duration; the second is standard.
        var program = lp.Program!;
        var baseEu = 100 * 10 * 0.9025 / 1000;
        var energy = program.Objectives[1].Coefficients.Select(e => e.Value).ToArray();
        Assert.Equal(3, energy.Length);
        Assert.Equal(baseEu, energy[0], 9);
        Assert.Equal(baseEu, energy[1], 9);
        Assert.Equal(baseEu * 2, energy[2], 9);
        Assert.Equal([5.0, 1.25, 0.625], program.Objectives[2].Coefficients.Select(e => e.Value));
    }

    [Fact]
    public void EnergyTargetAddsEuRowDrawAndGenerators()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ore"), Fx.Leaf("fuel")],
            Fx.Recipe("smelt", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));
        var machines = Fx.Machines(
            new()
            {
                ["Gen"] = [Fx.Block("gen-lv", tier: 1) with { GeneratorEfficiency = 100, GeneratorEuT = 32, GeneratorAmps = 1 }],
            },
            fuels: [new FactoryFuel("Gen", "fuel", 1, 100, null, null)]);
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph, new() { ["smelt"] = (40, 10, 1) }), machines, Fx.Costs(graph),
            Fx.Garage(defaultTier: 1), Fx.Weights(),
            new FactoryRequest(
                [
                    new FactoryTarget(FactoryTargetKind.Produce, "ingot", 1),
                    new FactoryTarget(FactoryTargetKind.Energy, null, 100),
                ],
                [], new Dictionary<string, string>()));

        var program = lp.Program!;
        // Rows: ingot target, EU export, ore, fuel. The run column draws 40 t x 10 EU/t / 20
        // = 20 EU/t per run rate; the generator nets 31 EU/t after the 1 EU/amp loss and
        // burns 32 x 20 / 100 = 6.4 fuel per second per machine.
        Assert.Equal(100, program.Rows[1].Lower);
        var run = program.Columns[0].Entries;
        Assert.Contains(new LpEntry(1, -20), run);
        var generator = program.Columns.Select((c, i) => (c, i))
            .First(pair => pair.c.Entries.Any(e => e is { Index: 1, Value: 31 }));
        Assert.Contains(new LpEntry(3, -6.4), generator.c.Entries);
        // Machines layer prices a generator as one busy machine.
        Assert.Contains(new LpEntry(generator.i, 1), lp.Program!.Objectives[2].Coefficients);
    }

    [Fact]
    public void EnergyBandCountsOnlySufficientTiers()
    {
        var graph = Fx.Graph([Fx.Leaf("fuel")]);
        var machines = Fx.Machines(
            new()
            {
                ["Gen"] =
                [
                    Fx.Block("gen-lv", tier: 1) with { GeneratorEfficiency = 100, GeneratorEuT = 32, GeneratorAmps = 1 },
                    Fx.Block("gen-hv", tier: 3, era: 3) with { GeneratorEfficiency = 100, GeneratorEuT = 512, GeneratorAmps = 1 },
                ],
            },
            fuels: [new FactoryFuel("Gen", "fuel", 1, 100, null, null)]);
        var lp = new RecordingLpSolver();

        Fx.Pipeline(lp).Solve(
            graph, Fx.Data(graph), machines, Fx.Costs(graph), Fx.Garage(defaultTier: 3), Fx.Weights(),
            new FactoryRequest(
                [new FactoryTarget(FactoryTargetKind.Energy, null, 512, GeneratorTier: 3)],
                [], new Dictionary<string, string>()));

        var program = lp.Program!;
        // Row 0 is the EU balance, row 1 the tier-3 band: both generators feed the balance,
        // only the HV one feeds the band.
        Assert.Equal(512, program.Rows[0].Lower);
        Assert.Equal(512, program.Rows[1].Lower);
        var feedsBalance = program.Columns.Count(c => c.Entries.Any(e => e.Index == 0 && e.Value > 0));
        var feedsBand = program.Columns.Count(c => c.Entries.Any(e => e.Index == 1 && e.Value > 0));
        Assert.Equal(2, feedsBalance);
        Assert.Equal(1, feedsBand);
    }
}
