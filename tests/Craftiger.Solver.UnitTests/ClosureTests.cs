using Craftiger.Solver.Services;

namespace Craftiger.Solver.UnitTests;

public sealed class ClosureTests
{
    private readonly ClosureService _service = new();

    [Fact]
    public void TheClosureCollectsMachinesDownToLeaves()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("dust", tier: 0)],
            Fx.Recipe("mix", machine: "Mixer", inputs: [("dust", 2)], outputs: ("blend", 1, 1.0)),
            Fx.Recipe("press", machine: "Forming Press", inputs: [("blend", 1)], outputs: ("plate", 1, 1.0)),
            Fx.Recipe("grind", machine: "Macerator", inputs: [("plate", 1)], outputs: ("dust", 1, 1.0)));

        var machines = _service.MachinesFor(graph, ["plate"]);

        Assert.Equal(["Forming Press", "Mixer"], machines);
    }

    [Fact]
    public void EveryProducerCountsNotJustTheCheapest()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("copper", tier: 0), Fx.Leaf("silver", tier: 1)],
            Fx.Recipe("cheap", machine: "Wiremill", inputs: [("copper", 1)], outputs: ("wire", 1, 1.0)),
            Fx.Recipe("dear", machine: "Extruder", inputs: [("silver", 1)], outputs: ("wire", 1, 1.0)));

        Assert.Equal(["Extruder", "Wiremill"], _service.MachinesFor(graph, ["wire"]));
    }

    [Fact]
    public void ALeafTargetNeedsNoMachines()
    {
        var graph = Fx.Graph(
            [Fx.Leaf("ingot", tier: 0)],
            Fx.Recipe("smelt", machine: "Furnace", inputs: [("ore", 1)], outputs: ("ingot", 1, 1.0)));

        Assert.Empty(_service.MachinesFor(graph, ["ingot"]));
    }
}
