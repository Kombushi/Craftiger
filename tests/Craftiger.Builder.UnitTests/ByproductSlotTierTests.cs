using Craftiger.Builder.Models.Dump;
using Xunit;

namespace Craftiger.Builder.UnitTests;

public sealed class ByproductSlotTierTests
{
    private static DumpRecipeMap Map(params DumpRecipeMapMachine[] machines) =>
        new("gt.recipe.test", "Test", 1, true, false, false, machines);

    private static DumpRecipeMapMachine Single(int tier, int slots) =>
        new($"i~m~{tier}", Multiblock: false, tier, Steam: false, slots);

    [Fact]
    public void TheMaceratorLadderDerivesItsThreeThresholds()
    {
        var map = Map(
            Single(1, 1), Single(2, 1), Single(3, 2), Single(4, 3),
            Single(5, 4), Single(6, 4), Single(7, 4));

        Assert.Equal([3, 4, 5], map.ByproductSlotTiers());
    }

    [Fact]
    public void AMapWhoseTiersAllCarryTheSameSlotsGatesNothing()
    {
        Assert.Null(Map(Single(1, 3), Single(2, 3), Single(3, 3)).ByproductSlotTiers());
    }

    [Fact]
    public void SteamAndMultiblockMachinesNeverShapeTheLadder()
    {
        var map = Map(
            new("i~steam", Multiblock: false, 1, Steam: true, 1),
            new("i~multi", Multiblock: true, null, Steam: false, 9),
            Single(1, 1), Single(3, 2));

        Assert.Equal([3], map.ByproductSlotTiers());
    }

    [Fact]
    public void MachinesWithoutSlotDataGateNothing()
    {
        Assert.Null(
            Map(new DumpRecipeMapMachine("i~m", Multiblock: false, 1, Steam: false, null))
                .ByproductSlotTiers());
    }
}
