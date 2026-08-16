namespace Craftiger.Solver.UnitTests;

public sealed class GarageLegalityTests
{
    [Fact]
    public void TheDefaultTierCoversUnlistedMachines()
    {
        var recipe = Fx.Recipe("r", machine: "Assembler", tier: 2);

        Assert.True(Fx.Legality().IsLegal(recipe, Fx.Garage(defaultTier: 2)));
        Assert.False(Fx.Legality().IsLegal(recipe, Fx.Garage(defaultTier: 1)));
    }

    [Fact]
    public void ANoneOverrideDisownsAMachineWhateverTheDefault()
    {
        var recipe = Fx.Recipe("r", machine: "Extruder", tier: 0);
        var garage = Fx.Garage(defaultTier: 14, tiers: new() { ["Extruder"] = null });

        Assert.False(Fx.Legality().IsLegal(recipe, garage));
    }

    [Fact]
    public void AlwaysOwnedMachinesShrugOffNone()
    {
        var recipe = Fx.Recipe("r", machine: "Furnace", tier: 0);
        var garage = Fx.Garage(defaultTier: 0, tiers: new() { ["Furnace"] = null });

        Assert.True(Fx.Legality().IsLegal(recipe, garage));
    }

    [Fact]
    public void AMixedMapRecipeWaitsForItsMultiblock()
    {
        var recipe = Fx.Recipe("r", machine: "Macerator", tier: 3, multiTier: 2);
        var below = Fx.Garage(defaultTier: 2);
        var built = Fx.Garage(defaultTier: 2, built: ["Macerator"]);

        Assert.False(Fx.Legality().IsLegal(recipe, below));
        Assert.True(Fx.Legality().IsLegal(recipe, built));
    }

    [Fact]
    public void HeatAboveTheInstalledCoilIsIllegal()
    {
        var recipe = Fx.Recipe("r", machine: "Vacuum Furnace", tier: 1, heat: 2000);
        var kanthal = Fx.Garage(defaultTier: 5, coils: new() { ["Vacuum Furnace"] = 2700 });
        var cupronickel = Fx.Garage(defaultTier: 5, coils: new() { ["Vacuum Furnace"] = 1800 });

        Assert.True(Fx.Legality().IsLegal(recipe, kanthal));
        Assert.False(Fx.Legality().IsLegal(recipe, cupronickel));
    }

    [Fact]
    public void NoInstalledCoilMeansNoHeatedRecipes()
    {
        var recipe = Fx.Recipe("r", machine: "Digester", tier: 1, heat: 800);

        Assert.False(Fx.Legality().IsLegal(recipe, Fx.Garage(defaultTier: 5)));
    }

    [Fact]
    public void TheBlastFurnaceGainsHeatPerHatchTierAboveMv()
    {
        var recipe = Fx.Recipe("r", machine: "Blast Furnace", tier: 1, heat: 1900);
        var hv = Fx.Garage(defaultTier: 3, coils: new() { ["Blast Furnace"] = 1800 });
        var mv = Fx.Garage(defaultTier: 2, coils: new() { ["Blast Furnace"] = 1800 });

        Assert.True(Fx.Legality().IsLegal(recipe, hv));
        Assert.False(Fx.Legality().IsLegal(recipe, mv));
    }

    [Fact]
    public void OtherCoilMapsGetNoHatchBonus()
    {
        var recipe = Fx.Recipe("r", machine: "Vacuum Furnace", tier: 1, heat: 1900);
        var garage = Fx.Garage(defaultTier: 14, coils: new() { ["Vacuum Furnace"] = 1800 });

        Assert.False(Fx.Legality().IsLegal(recipe, garage));
    }

    [Fact]
    public void HeatExemptMachinesIgnoreHeatEntirely()
    {
        var recipe = Fx.Recipe("r", machine: "Helioflux Melting Core", tier: 11, heat: 100000);

        Assert.True(Fx.Legality().IsLegal(recipe, Fx.Garage(defaultTier: 11)));
    }
}
