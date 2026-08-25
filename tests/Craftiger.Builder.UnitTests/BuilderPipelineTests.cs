namespace Craftiger.Builder.UnitTests;

public sealed class BuilderPipelineTests : IClassFixture<BuilderPipelineFixture>
{
    private readonly BuilderPipelineFixture _fixture;

    public BuilderPipelineTests(BuilderPipelineFixture fixture) => _fixture = fixture;

    [Fact]
    public void BronzeSmeltsAtTierZeroDespiteBlockCycle() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.GtBronze}' AND tier = 0"));

    [Fact]
    public void AluminiumTiersFromMultiAmpEbfNotFromBlockCycle() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AluIngot}'"));

    [Fact]
    public void CropHarvestsInheritTheirUnderBlockEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.BerryIngot}'"));

    [Fact]
    public void CropDropsClassifyAsCropDrops() =>
        Assert.Equal("crop_drop", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Berry}'"));

    [Fact]
    public void HiddenCropsProduceNoHarvestRecipe() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id LIKE 'cnh~%'"));

    [Fact]
    public void UndergroundFluidsWaitForTheirPumpEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.OilIngot}'"));

    [Fact]
    public void PumpedFluidsAreWorldFluidsButNeverPriceThemselves()
    {
        Assert.Equal("world_fluid", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Oil}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipes WHERE id = 'gtuf~{FixtureDump.Oil}'"));
    }

    [Fact]
    public void EndStoneSeedsAtItsOwnEra() =>
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.EndIngot}'"));

    [Fact]
    public void GemsTierByTheirProductionEra() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.Gem}'"));

    [Fact]
    public void DustsInheritTheirGemTwinTier() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.GemDust}'"));

    [Fact]
    public void NuggetsAreLeavesOfTheirOwnClass() =>
        Assert.Equal("nugget", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.NugNugget}'"));

    [Fact]
    public void OreProcessingIntermediatesAreNeverLeaves() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.NugImpure}'"));

    [Fact]
    public void CropDropsAnotherRecipeMakesAreNotLeaves() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.ClayBall}'"));

    [Fact]
    public void TieredLeavesTheEraSolveNeverReachedAreDropped() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(leaf_class) FROM items WHERE id = '{FixtureDump.LostIngot}'"));

    [Fact]
    public void WorldFluidsCarryTheirOwnWeight() =>
        Assert.Equal(8.0, _fixture.Scalar<double>(
            $"SELECT weight FROM item_weights WHERE item_id = '{FixtureDump.Oil}'"));

    [Fact]
    public void ClayBallPricesFromBreakingItsBlock()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'bd~minecraft:clay~0' AND item_id = '{FixtureDump.ClayBlock}'"));
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_outputs WHERE recipe_id = 'bd~minecraft:clay~0' AND item_id = '{FixtureDump.ClayBall}'"));
    }

    [Fact]
    public void OredictlessMinableBlocksStillSeedTheirDrops() =>
        Assert.Equal("minable_block", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.ClayBlock}'"));

    [Fact]
    public void BlocksDroppingThemselvesMakeNoRecipe() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE id = 'bd~minecraft:obsidian~0'"));

    [Fact]
    public void MixedMapsDiscountOnceTheirMultiblockIsReachable() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.MixIngot}'"));

    [Fact]
    public void MixedMapsKeepFullTierWhileOnlyTheirSingleBlocksAreAffordable() =>
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DearIngot}'"));

    [Fact]
    public void MixedMapsShipATierForEachKindOfMachine()
    {
        Assert.Equal(3, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_mix'"));
        Assert.Equal(2, _fixture.Scalar<int>("SELECT multi_tier FROM recipes WHERE id = 'r_mix'"));
    }

    [Fact]
    public void MultiblockOnlyMapsCarryTheAllowanceInTheirOwnTier()
    {
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_ebf'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(multi_tier) FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void SingleBlockOnlyMapsHaveNoMultiblockTier() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(multi_tier) FROM recipes WHERE id = 'r_macerate'"));

    [Fact]
    public void OresTheWorldNeverPlacesSeedNothing() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.PhantomIngot}'"));

    [Fact]
    public void RecyclingAManufacturedItemNeverReachesTheArtifact() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_recycle'"));

    [Fact]
    public void RecyclingOneShapeOfAMaterialIntoAnotherSurvives() =>
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_melt'"));

    [Fact]
    public void RecyclingAWireSurvivesByGtsPrefixFlags() =>
        Assert.Equal(1, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_wire_recycle'"));

    [Fact]
    public void RecyclingAContainerNeverReachesTheArtifact() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_cell_recycle'"));

    [Fact]
    public void GemGradesPriceAsFractionsOfTheirGem()
    {
        Assert.Equal("gem_flawed", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(FixtureDump.Gem, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(2.0, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.FlawedGem}'"));
        Assert.Equal(0.25, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.ExquisiteGem}'"));
    }

    [Fact]
    public void FallbackTiersComeFromRealRecipesNotPilePacking() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.InertDust}'"));

    [Fact]
    public void EveryMachineShipsItsAvailabilityEra()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipes WHERE machine NOT IN (SELECT machine FROM machine_eras)"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT era FROM machine_eras WHERE machine = 'Crafting Table'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT era IS NULL FROM machine_eras WHERE machine = 'Electric Blast Furnace'"));
    }

    [Fact]
    public void OnlyMultiblockOnlyMapsFlagAsMultiblocks()
    {
        Assert.True(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Electric Blast Furnace'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Mixer'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Macerator'"));
        Assert.False(_fixture.Scalar<bool>(
            "SELECT multiblock FROM machine_eras WHERE machine = 'Crafting Table'"));
    }

    [Fact]
    public void ArtifactStampsItsSchemaVersion()
    {
        Assert.Equal(12, Repositories.PlannerRepository.SchemaVersion);
        Assert.Equal("12", _fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'schema_version'"));
    }

    [Fact]
    public void ArtifactStampsAUniqueBuildId() =>
        Assert.Matches("^[0-9a-f]{32}$", _fixture.Scalar<string>("SELECT value FROM meta WHERE key = 'build_id'"));

    [Fact]
    public void ArtifactIndexesCaseFoldedSearchText()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_search WHERE item_id = '{FixtureDump.GtBronze}' AND text = 'ingotbronze'"));
        Assert.Equal(
            _fixture.Scalar<int>("SELECT COUNT(*) FROM items") + _fixture.Scalar<int>("SELECT COUNT(*) FROM item_aliases"),
            _fixture.Scalar<int>("SELECT COUNT(*) FROM item_search"));
        Assert.Equal(FixtureDump.GtBronze, _fixture.Scalar<string>(
            "SELECT item_id FROM item_search WHERE item_search MATCH '\"otbro\"'"));
    }

    [Fact]
    public void ArtifactCarriesQueryStatistics() =>
        Assert.True(_fixture.Scalar<int>("SELECT COUNT(*) FROM sqlite_stat1") > 0);

    [Fact]
    public void FractionLeavesShipTheirParentLink()
    {
        Assert.Equal(FixtureDump.NugIngot, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.NugNugget}'"));
        Assert.Equal(9.0, _fixture.Scalar<double>(
            $"SELECT divisor FROM item_parents WHERE item_id = '{FixtureDump.NugNugget}'"));
        Assert.Equal(FixtureDump.InertDust, _fixture.Scalar<string>(
            $"SELECT parent_item_id FROM item_parents WHERE item_id = '{FixtureDump.InertSmall}'"));
    }

    [Fact]
    public void EveryShippedFractionHasAParentRow() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM items WHERE leaf_class IN ('dust_small', 'dust_tiny', 'nugget', " +
            "'gem_chipped', 'gem_flawed', 'gem_flawless', 'gem_exquisite') " +
            "AND id NOT IN (SELECT item_id FROM item_parents)"));

    [Fact]
    public void NoLeafPricesFarBelowItsOwnWeight()
    {
        Assert.Equal(0, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_leaks'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_free_items'"));
        Assert.Equal(1, _fixture.Scalar<int>("SELECT value FROM meta WHERE key = 'price_converged'"));
    }

    [Fact]
    public void EbfRecipeKeepsHeat() =>
        Assert.Equal(1700, _fixture.Scalar<int>("SELECT heat FROM recipes WHERE id = 'r_ebf'"));

    [Fact]
    public void ChancedOutputKeepsChance() =>
        Assert.Equal(0.9, _fixture.Scalar<double>(
            "SELECT chance FROM recipe_outputs WHERE recipe_id = 'r_macerate' AND chance < 1"), precision: 9);

    [Fact]
    public void CatalystsShipAsDisplayOnlyRows()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_inputs WHERE item_id IN ('{FixtureDump.Saw}', '{FixtureDump.Mold}') AND catalyst = 0"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 0"));
        Assert.Equal(FixtureDump.Saw, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 1"));
        Assert.Equal(FixtureDump.Mold, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.Saw}'"));
    }

    [Fact]
    public void AMetaWearingToolCondemnsItsSlot() =>
        Assert.Equal(FixtureDump.TinkerSaw, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_tinker_cut' AND catalyst = 1"));

    [Fact]
    public void OnlyWearingToolsCarryTheToolFlag()
    {
        // Both saws wear; the mold is a catalyst that never does. In a mixed slot the flag is
        // per member: the saw carries it, the ingot beside it does not.
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_planks' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tinker_cut' AND catalyst = 1"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_extrude' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND item_id = '{FixtureDump.Saw}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tool FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND item_id = '{FixtureDump.IronIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE catalyst = 0 AND tool = 1"));
    }

    [Fact]
    public void ItemsCarryTheirStackSizeAndFluidsNone()
    {
        Assert.Equal(64, _fixture.Scalar<int>($"SELECT max_stack FROM items WHERE id = '{FixtureDump.IronIngot}'"));
        Assert.Equal(1, _fixture.Scalar<int>($"SELECT max_stack FROM items WHERE id = '{FixtureDump.Saw}'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(max_stack) FROM items WHERE is_fluid = 1"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM items WHERE is_fluid = 0 AND max_stack IS NULL"));
    }

    [Fact]
    public void ShapedRecipesKeepTheirGridOverTheFoldedSlots()
    {
        // r_planks: the log in cell 0 is ingredient slot 0, the saw in cell 1 the catalyst slot
        // after it; r_tool_choice puts the tool slot first on the grid and the log second; a
        // choice slot is addressed by its own number; furnace recipes have no shape.
        Assert.Equal("0:0,1:1", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM (SELECT cell, slot FROM recipe_grid WHERE recipe_id = 'r_planks' ORDER BY cell)"));
        Assert.Equal("0:1,1:0", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM (SELECT cell, slot FROM recipe_grid WHERE recipe_id = 'r_tool_choice' ORDER BY cell)"));
        Assert.Equal("0:0", _fixture.Scalar<string>(
            "SELECT GROUP_CONCAT(cell || ':' || slot, ',') FROM recipe_grid WHERE recipe_id = 'r_any_iron_use'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_grid g JOIN recipes r ON r.id = g.recipe_id WHERE r.machine <> 'Crafting Table'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_grid g WHERE NOT EXISTS (SELECT 1 FROM recipe_inputs i WHERE i.recipe_id = g.recipe_id AND i.slot = g.slot)"));
    }

    [Fact]
    public void AContainerReturningItemStaysAnIngredient() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_soup' AND catalyst = 1"));

    [Fact]
    public void ChoiceSlotsShipEveryAlternativeUnderOneSlot()
    {
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_any_iron_use'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_any_iron_use'"));
    }

    [Fact]
    public void OneToolCondemnsTheWholeSlot()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 0"));
        Assert.Equal(FixtureDump.Log, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 0"));
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 1"));
        Assert.Equal(1, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_tool_choice' AND catalyst = 1"));
    }

    [Fact]
    public void ConcreteInputsAndChoicesGetSeparateSlots()
    {
        Assert.Equal(3, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_mixed_slots'"));
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(DISTINCT slot) FROM recipe_inputs WHERE recipe_id = 'r_mixed_slots'"));
    }

    [Fact]
    public void OredictEquivalentIngotsUnifyToOneCanonicalItem()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.Ic2Bronze}'"));
        Assert.Equal(FixtureDump.GtBronze, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_macerate'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_aliases WHERE item_id = '{FixtureDump.GtBronze}' AND alias = 'ingotBronze'"));
    }

    [Fact]
    public void MachineAvailabilityGatesRecipeEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ColdIngot}'"));

    [Fact]
    public void OffworldOresSeedAtTheirDimensionEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.NaqIngot}'"));

    [Fact]
    public void SpaceMiningGatesEraButNeverPrices()
    {
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.KobIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_space'"));
    }

    [Fact]
    public void RawChunksSeedAtTheirVeinEra() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.RuniteIngot}'"));

    [Fact]
    public void WirelessRecipesTakeNoVoltageTier() =>
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.WirelessIngot}' AND tier = 0"));

    [Fact]
    public void StoneVariantsSeedOnlyInTheirOwnDimensions() =>
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DualIngot}'"));

    [Fact]
    public void OresWithoutWorldgenNeverSeedAnEra() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ComIngot}'"));

    [Fact]
    public void MachineInputVoltageFloorsRecipeEra() =>
        Assert.Equal(2, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.DryIngot}'"));

    [Fact]
    public void MaceratorByproductSlotsOpenByTier()
    {
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_outputs WHERE recipe_id = 'r_cu_macerate' AND item_id = '{FixtureDump.ByDust}'"));
        Assert.Equal(3, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_cu_macerate~b3'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM recipe_outputs WHERE recipe_id = 'r_cu_macerate~b3' AND item_id = '{FixtureDump.ByDust}'"));
        Assert.Equal(3, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.ByIngot}'"));
    }

    [Fact]
    public void DustsInheritTheirIngotTier()
    {
        Assert.Equal(4, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.NaqDust}'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AluDust}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM item_tiers WHERE item_id = '{FixtureDump.BronzeDust}' AND tier != 0"));
    }

    [Fact]
    public void DerivedDustsInheritEraInsteadOfSeedingZero()
    {
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.AnnealedIngot}'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.CopperIngot}'"));
    }

    [Fact]
    public void WildcardOredictsDoNotUnify()
    {
        Assert.Equal(FixtureDump.IronIngot, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_iron_use'"));
        Assert.Equal(FixtureDump.CastIron, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_cast_use'"));
    }

    [Fact]
    public void TheCanonicalIsGtsUnificationTarget()
    {
        Assert.Equal(FixtureDump.TargetGt, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_target_use'"));
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.TargetVanilla}'"));
    }

    [Fact]
    public void BlacklistedMembersKeepTheirIdentity()
    {
        Assert.Equal(2, _fixture.Scalar<int>(
            "SELECT COUNT(*) FROM recipe_inputs WHERE recipe_id = 'r_blackium_use'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"SELECT COUNT(*) FROM items WHERE id = '{FixtureDump.BlackMetal}'"));
    }

    [Fact]
    public void ConventionPrefixesNeverClassifyMaterials() =>
        Assert.Null(_fixture.Scalar<string?>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.PlanetDust}'"));

    [Fact]
    public void NonUnifiedNamesClassifyWithoutMerging()
    {
        Assert.Equal(FixtureDump.CherryLeaves, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_petals'"));
        Assert.Equal(FixtureDump.OakLeaves, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_oak_leaves_use'"));
        Assert.Equal("farmable", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.CherryLeaves}'"));
        Assert.Equal("farmable", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.OakLeaves}'"));
    }

    [Fact]
    public void MachineNamesAreNormalized()
    {
        Assert.Equal("Macerator", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_macerate'"));
        Assert.Equal("Crafting Table", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_block'"));
        Assert.Equal("Electric Blast Furnace", _fixture.Scalar<string>("SELECT machine FROM recipes WHERE id = 'r_ebf'"));
    }

    [Fact]
    public void FuelTabsAreExcluded() =>
        Assert.Equal(0, _fixture.Scalar<int>("SELECT COUNT(*) FROM recipes WHERE id = 'r_fuel'"));

    [Fact]
    public void SteamMachinesRelaxTheirMapsRecipesToEraZero() =>
        Assert.Equal(0, _fixture.Scalar<int>(
            $"SELECT tier FROM item_tiers WHERE item_id = '{FixtureDump.SteamIngot}'"));

    [Fact]
    public void VoltageTiersFollowTheLadder()
    {
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_ebf'"));
        Assert.Equal(1, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_macerate'"));
        Assert.Equal(0, _fixture.Scalar<int>("SELECT tier FROM recipes WHERE id = 'r_smelt'"));
    }

    [Fact]
    public void FilledCellsDecomposeIntoFluidAndNetOut()
    {
        Assert.Equal(FixtureDump.Water, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_inputs WHERE recipe_id = 'r_electrolyze'"));
        Assert.Equal(1000, _fixture.Scalar<int>(
            "SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_electrolyze'"));
        Assert.Equal(FixtureDump.Hydrogen, _fixture.Scalar<string>(
            "SELECT item_id FROM recipe_outputs WHERE recipe_id = 'r_electrolyze'"));
    }

    [Fact]
    public void AFluidSlotWithAlternativesShipsEachAtItsOwnAmount()
    {
        Assert.Equal(144, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_fluid_choice' AND item_id = '{FixtureDump.Water}'"));
        Assert.Equal(1000, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_fluid_choice' AND item_id = '{FixtureDump.Oxygen}'"));
        Assert.Equal(1, _fixture.Scalar<int>(
            $"""
             SELECT COUNT(DISTINCT slot) FROM recipe_inputs
             WHERE recipe_id = 'r_fluid_choice'
               AND item_id IN ('{FixtureDump.Water}', '{FixtureDump.Oxygen}')
             """));
    }

    [Fact]
    public void FluidInputsCarryMillibuckets() =>
        Assert.Equal(1000, _fixture.Scalar<int>(
            $"SELECT amount FROM recipe_inputs WHERE recipe_id = 'r_solidify' AND item_id = '{FixtureDump.Water}'"));

    [Fact]
    public void LeafClassesFollowOredict()
    {
        Assert.Equal("ingot", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.GtBronze}'"));
        Assert.Equal("dust", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.BronzeDust}'"));
        Assert.Equal("log", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Log}'"));
        Assert.Equal("world_fluid", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.Water}'"));
        Assert.Equal("minable_block", _fixture.Scalar<string>(
            $"SELECT leaf_class FROM items WHERE id = '{FixtureDump.ObsidianBlock}'"));
    }
}
