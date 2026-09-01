using Microsoft.Data.Sqlite;

namespace Craftiger.Builder.UnitTests;

/// <summary>Recipe types, recipe maps, and every production recipe the builder rules are judged on.</summary>
public static partial class FixtureDump
{
    private static void AddRecipes(SqliteConnection db)
    {
        RecipeType(db, "t_shaped", "minecraft", "Crafting (Shaped)", shapeless: false);
        RecipeType(db, "t_furnace", "minecraft", "Furnace");
        RecipeType(db, "rt~gregtech~gt.recipe.blastfurnace~MV", "gregtech", "Blast Furnace (MV)", handlerIcons: 2);
        RecipeType(db, "rt~gregtech~gt.recipe.extruder~MV", "gregtech", "Extruder (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.macerator~ULV", "gregtech", "Macerator (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.fluidsolidifier~MV", "gregtech", "Fluid Solidifier (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.largeboilerfakefuels~ULV", "gregtech", "Large Boiler Fuels (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.gasturbinefuel~ULV", "gregtech", "Gas Turbine Fuel (ULV)");
        RecipeType(db, "rt~gregtech~gtpp.recipe.RTGgenerators~ULV", "gregtech", "RTG (ULV)");
        RecipeType(db, "rt~gregtech~gg.recipe.naquadah_reactor~ULV", "gregtech", "Large Naquadah Reactor (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.semifluidboilerfuels~ULV", "gregtech", "Semifluid Boiler (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.fixturegrinder~LV", "gregtech", "Bronze Grinder (LV)");
        RecipeType(db, "rt~gregtech~gt.recipe.electrolyzer~MV", "gregtech", "Electrolyzer (MV)");
        RecipeType(db, "rt~gregtech~gt.recipe.arcfurnace~LV", "gregtech", "Arc Furnace (LV)");
        RecipeType(db, "rt~gregtech~gt.recipe.fluidextractor~LV", "gregtech", "Fluid Extractor (LV)");
        RecipeType(db, "rt~gregtech~gt.recipe.alloysmelter~ULV", "gregtech", "Alloy Smelter (ULV)");
        RecipeType(db, "rt~gregtech~gt.recipe.vacuumfreezer~MV", "gregtech", "Vacuum Freezer (MV)", handlerIcons: 0, handlerItem: FreezerItem);
        RecipeType(db, "rt~gregtech~gt.recipe.spacemining~HV", "gregtech", "Space Mining (HV)", handlerIcons: 0, handlerItem: SpaceMiner);
        RecipeType(db, "rt~gregtech~gt.recipe.dryer~LV", "gregtech", "Dryer (LV)", handlerIcons: 0, handlerItem: Dryer);
        RecipeType(db, "rt~gregtech~gt.recipe.mixer~HV", "gregtech", "Mixer (HV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.dearmixer~HV", "gregtech", "Dear Mixer (HV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.packager~ULV", "gregtech", "Packager (ULV)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gt.recipe.mixer~MAX", "gregtech", "Mixer (MAX)", handlerIcons: 0);
        RecipeType(db, "rt~gregtech~gtpp.recipe.treefarm~ULV", "gregtech", "Tree Growth Simulator (ULV)", handlerIcons: 0);

        RecipeMap(db, "gt.recipe.blastfurnace", "Blast Furnace", [(EbfController, true, null, false, null)]);
        RecipeMap(db, "gt.recipe.macerator", "Macerator", [(MaceratorLv, false, 1, false, 1), (MaceratorHv, false, 3, false, 2)]);
        RecipeMap(db, "gt.recipe.mixer", "Mixer", [(MixerLv, false, 1, false, null), (MixerStack, true, null, false, null)]);
        RecipeMap(db, "gt.recipe.dearmixer", "Dear Mixer", [(MixerLv, false, 1, false, null), (DearStack, true, null, false, null)]);
        // The fuel flag alone drops the tab; the machine name says nothing about fuels.
        RecipeMap(db, "gt.recipe.largeboilerfakefuels", "Large Boiler", [], isFuel: true);
        RecipeMap(db, "gt.recipe.gasturbinefuel", "Gas Turbine Fuel",
            [(DeadTurbine, true, null, false, null), (LiveTurbine, true, null, false, null), (XlTurbine, true, null, false, null)],
            isFuel: true);
        RecipeMap(db, "gtpp.recipe.RTGgenerators", "RTG", [], isFuel: true);
        RecipeMap(db, "gg.recipe.naquadah_reactor", "Large Naquadah Reactor",
            [(LargeNaquadahReactor, true, null, false, null), (LargeCombustionEngine, true, null, false, null)],
            isFuel: true);
        RecipeMap(db, "gt.recipe.semifluidboilerfuels", "Semifluid Boiler", [], isFuel: true);
        // A steam machine relaxes its map's LV recipes; the flag decides, never the name.
        RecipeMap(db, "gt.recipe.fixturegrinder", "Bronze Grinder", [(SteamGrinder, false, 1, true, null)]);
        RecipeMap(db, "gtpp.recipe.treefarm", "Tree Growth Simulator", [(TreeFarm, true, null, false, null)]);

        // The tree farm's NEI row: no inputs, the sapling in the controller slot, one output per mode slot.
        Recipe(db, "r_tree_oak", "rt~gregtech~gtpp.recipe.treefarm~ULV", inputs: [], outputs: [(Log, 2, 1.0)],
            byproducts: [(OakSapling, 5, 1.0, 1), (OakLeaves, 2, 1.0, 2)], voltage: 0, duration: 100, label: "ULV");
        SpecialItem(db, "r_tree_oak", OakSapling);
        Recipe(db, "r_tree_farm_craft", "t_shaped", inputs: [("g_log", 0)], outputs: [(TreeFarm, 1, 1.0)]);
        // A log no tree farm grows stays a primitive the world hands over.
        Recipe(db, "r_pine_planks", "t_shaped", inputs: [("g_pine_log", 0)], outputs: [(Plank, 4, 1.0)]);

        // Ingot <-> block cycle, both directions on the crafting table.
        Recipe(db, "r_block", "t_shaped", inputs: [("g_bronze_ingot9", 0)], outputs: [(BronzeBlock, 1, 1.0)]);
        Recipe(db, "r_unblock", "t_shaped", inputs: [("g_bronze_block", 0)], outputs: [(GtBronze, 9, 1.0)]);
        Recipe(db, "r_alu_block", "t_shaped", inputs: [("g_alu_ingot9", 0)], outputs: [(AluBlock, 1, 1.0)]);
        Recipe(db, "r_alu_unblock", "t_shaped", inputs: [("g_alu_block", 0)], outputs: [(AluIngot, 9, 1.0)]);

        // Smelting bronze dust is the tier-0 route; EBF is aluminium's only real route.
        Recipe(db, "r_smelt", "t_furnace", inputs: [("g_bronze_dust", 0)], outputs: [(GtBronze, 1, 1.0)]);
        Recipe(db, "r_ebf", "rt~gregtech~gt.recipe.blastfurnace~MV", inputs: [("g_alu_dust", 0)], outputs: [(AluIngot, 1, 1.0)], voltage: 120, duration: 500, heat: 1700);

        // Chanced byproduct plus a saw catalyst on the grid.
        Recipe(db, "r_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_bronze_ingot", 0)], outputs: [(BronzeDust, 1, 1.0), (BronzeDust, 1, 0.9)], voltage: 4, duration: 100, amperage: 2);
        Recipe(db, "r_planks", "t_shaped", inputs: [("g_log", 0), ("g_saw", 1)], outputs: [(Plank, 4, 1.0)]);

        // Extruder-only rod with a zero-size shape mold; solidifier gives a pinnable alternative.
        Recipe(db, "r_extrude", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_alu_ingot", 0), ("g_mold", 1)], outputs: [(AluRod, 2, 1.0)], voltage: 96, duration: 200);
        // A strictly-worse duplicate route exercises the cleanroom/low-gravity columns without ever winning.
        Recipe(db, "r_flags", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_alu_ingot", 0), ("g_mold", 1)], outputs: [(AluRod, 2, 1.0)], voltage: 96, duration: 400, cleanroom: true, lowGravity: true);
        Recipe(db, "r_solidify", "rt~gregtech~gt.recipe.fluidsolidifier~MV", inputs: [("g_alu_ingot", 0)], outputs: [(AluRod, 1, 1.0)], voltage: 24, duration: 100, fluidInputs: [("g_water", 0)]);

        // Distinct materials share the wildcard ingotAnyIron group but must stay separate.
        Recipe(db, "r_iron_use", "t_shaped", inputs: [("g_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_cast_use", "t_shaped", inputs: [("g_cast_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        // Names GT never unifies (treeLeaves) classify their members but never merge them.
        Recipe(db, "r_oak_leaves_use", "t_shaped", inputs: [("g_oak_leaves", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_petals", "t_shaped", inputs: [("g_cherry_leaves", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_target_use", "t_shaped", inputs: [("g_targetium", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_blackium_use", "t_shaped", inputs: [("g_blackium", 0)], outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_planet_use", "t_shaped", inputs: [("g_planet_dust", 0)], outputs: [(Plank, 1, 1.0)]);
        // The pipe extrudes four to the ingot, so grinding one back to a full dust amplifies
        // fourfold; shaving it to a small pile is exact and survives, as does the grid consumer.
        Recipe(db, "r_pipe_extrude", "rt~gregtech~gt.recipe.extruder~MV",
            inputs: [("g_bronze_ingot", 0), ("g_mold", 1)], outputs: [(FixturePipe, 4, 1.0)], voltage: 96, duration: 200);
        Recipe(db, "r_pipe_grind", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_fixture_pipe", 0)], outputs: [(BronzeDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_pipe_shave", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_fixture_pipe", 0)], outputs: [(BronzeSmall, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_pipe_block", "t_shaped", inputs: [("g_fixture_pipe", 0)], outputs: [(Plank, 1, 1.0)]);
        // A shard nothing produces has unprovable content, so its grind is innocent.
        Recipe(db, "r_shard_grind", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_shard", 0)], outputs: [(ShardDust, 1, 1.0)], voltage: 4, duration: 100);
        // Fluids count at molten density: a whiff of gas cannot launder the grind, while
        // a full molten measure honestly carries its matter in.
        FluidStack(db, "g_whiff", 4, Oxygen);
        FluidStack(db, "g_molten", 144, Water);
        Recipe(db, "r_pipe_gasarc", "rt~gregtech~gt.recipe.arcfurnace~LV",
            inputs: [("g_fixture_pipe", 0)], outputs: [(BronzeSmall, 2, 1.0)], voltage: 30, duration: 100,
            fluidInputs: [("g_whiff", 0)]);
        Recipe(db, "r_pipe_infuse", "rt~gregtech~gt.recipe.fluidsolidifier~MV",
            inputs: [("g_fixture_pipe", 0)], outputs: [(ShardDust, 1, 1.0)], voltage: 24, duration: 100,
            fluidInputs: [("g_molten", 0)]);
        // Clay is world-minable: grinding it is primary production even though its one
        // crafting recipe holds half the matter the grind hands out.
        Recipe(db, "r_clay_make", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(ClayBlock, 1, 1.0)]);
        Recipe(db, "r_clay_grind", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_clay_block", 0)], outputs: [(ClayDust, 2, 1.0)], voltage: 4, duration: 100);
        // GT's own composition record bounds the box: two ingots and a byproduct in, so
        // four dusts out is a lie while two dusts plus the byproduct is honest.
        ItemData(db, DataBox, "Bronze", 7257600, ("Shardium", 3628800));
        ItemData(db, DataGhost, "Bronze", -1);
        Recipe(db, "r_databox_grind", "rt~gregtech~gt.recipe.arcfurnace~LV",
            inputs: [("g_databox", 0)], outputs: [(BronzeDust, 4, 1.0)], voltage: 30, duration: 100);
        Recipe(db, "r_databox_shred", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_databox", 0)], outputs: [(BronzeDust, 2, 1.0), (ShardDust, 1, 1.0)], voltage: 4, duration: 100);
        // An undefined amount is unknown, never zero: the ghost's grind stays.
        Recipe(db, "r_ghost_grind", "rt~gregtech~gt.recipe.macerator~ULV",
            inputs: [("g_dataghost", 0)], outputs: [(BronzeDust, 1, 1.0)], voltage: 4, duration: 100);
        // A slot that takes either iron must ship both, not whichever sorts first.
        Recipe(db, "r_any_iron_use", "t_shaped", inputs: [("g_any_iron", 0)], outputs: [(Plank, 1, 1.0)]);
        // A tool anywhere in a slot marks the whole slot as tools, third-party ones included.
        Group(db, "g_saw_or_iron", (Saw, 1), (IronIngot, 1));
        Recipe(db, "r_tool_choice", "t_shaped", inputs: [("g_saw_or_iron", 0), ("g_log", 1)],
            outputs: [(Plank, 1, 1.0)]);
        // Tools announce themselves by crafting into their own worn selves: the GT saw keeps
        // its damage in NBT, the fixture saw wears through its meta.
        ItemContainer(db, Saw, Saw + "~worn");
        ItemContainer(db, TinkerSaw, "i~tconstruct~fixture_saw~4");
        // A soup bucket leaves a bucket behind, which is a different item: really consumed.
        ItemContainer(db, SoupBucket, "i~minecraft~bucket~0");
        Group(db, "g_tinker_saw", (TinkerSaw, 1));
        Group(db, "g_soup", (SoupBucket, 1));
        Recipe(db, "r_tinker_cut", "t_shaped", inputs: [("g_tinker_saw", 0), ("g_log", 1)],
            outputs: [(Plank, 1, 1.0)]);
        Recipe(db, "r_soup", "t_shaped", inputs: [("g_soup", 0)], outputs: [(Plank, 1, 1.0)]);
        // A concrete input and a choice must not share a slot number.
        Recipe(db, "r_mixed_slots", "t_shaped", inputs: [("g_log", 0), ("g_any_iron", 1)],
            outputs: [(Plank, 1, 1.0)]);

        // Annealed copper: real era is the LV arc route; the dust-smelting loop
        // must inherit it rather than grant era 0. The slot-1 byproduct only
        // exists on HV+ macerators, splitting the recipe into tiered variants.
        Recipe(db, "r_cu_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 2, 1.0)], voltage: 4, duration: 100,
            byproducts: [(ByDust, 1, 0.5, 1)]);
        Recipe(db, "r_by_smelt", "t_furnace", inputs: [("g_by_dust", 0)], outputs: [(ByIngot, 1, 1.0)]);

        // The dryer is buildable at era 0 but runs on MV voltage.
        Recipe(db, "r_dryer_craft", "t_shaped", inputs: [("g_log", 0)], outputs: [(Dryer, 1, 1.0)]);
        Recipe(db, "r_berry_seed_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(BerrySeed, 1, 1.0)]);
        Recipe(db, "r_berry_press", "t_furnace", inputs: [("g_berry", 0)], outputs: [(BerryIngot, 1, 1.0)]);

        // Oil lies in the Overworld, but only a drilling rig gets it out.
        Recipe(db, "r_rig_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(Rig, 1, 1.0)]);
        Recipe(db, "r_oil_smelt", "t_furnace", inputs: [], outputs: [(OilIngot, 1, 1.0)], fluidInputs: [("g_oil", 0)]);

        // End Stone is only minable once the End is open, so what it smelts into waits too.
        Recipe(db, "r_end_smelt", "t_furnace", inputs: [("g_endstone", 0)], outputs: [(EndIngot, 1, 1.0)]);

        // A gem has no ingot twin: its era comes from cutting it, and its dust inherits that.
        Recipe(db, "r_gem_cut", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_gem_ore", 0)],
            outputs: [(Gem, 1, 1.0)], voltage: 120, duration: 100);
        Recipe(db, "r_gem_grind", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_gem", 0)],
            outputs: [(GemDust, 1, 1.0)], voltage: 4, duration: 100);
        // Gem grades are leaves priced as fractions of their gem via GT's material amounts.
        Recipe(db, "r_gem_crack", "t_shaped", inputs: [("g_gem", 0)], outputs: [(FlawedGem, 2, 1.0)]);
        Recipe(db, "r_gem_polish", "t_shaped", inputs: [("g_gem", 0)], outputs: [(ExquisiteGem, 1, 1.0)]);
        // Nugium covers the derived and intermediate leaf rules: a nugget priced off its
        // ingot, and an ore-washing pile that has to price from its own recipe.
        Recipe(db, "r_nug_smelt", "t_furnace", inputs: [("g_copper_ingot", 0)], outputs: [(NugIngot, 1, 1.0)]);
        Recipe(db, "r_nug_split", "t_shaped", inputs: [("g_nug_ingot", 0)], outputs: [(NugNugget, 9, 1.0)]);
        Recipe(db, "r_nug_wash", "t_furnace", inputs: [("g_nug_impure", 0)], outputs: [(NugIngot, 1, 1.0)]);
        Recipe(db, "r_nug_grind", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_nug_ingot", 0)],
            outputs: [(NugImpure, 1, 1.0)], voltage: 4, duration: 100);
        // Lostium is only ever consumed, so the era solve never reaches it.
        Recipe(db, "r_lost_use", "t_furnace", inputs: [("g_lost_ingot", 0)], outputs: [(IronIngot, 1, 1.0)]);
        // Melting a manufactured item down gives back more than crafting it cost. The widget
        // carries no oredict, and its recipe takes either form of aluminium — a choice the
        // conservation bound cannot price — so only the recycling tag stands between it and
        // a leak.
        Group(db, "g_alu_either", (AluIngot, 1), (AluBlock, 1));
        Recipe(db, "r_widget", "t_shaped", inputs: [("g_alu_either", 0)], outputs: [(FixtureWidget, 1, 1.0)]);
        Recipe(db, "r_recycle", "rt~gregtech~gt.recipe.arcfurnace~LV", inputs: [("g_widget", 0)],
            outputs: [(AluIngot, 6, 1.0)], voltage: 30, duration: 100, category: "arcFurnaceRecycling");
        // Melting one shape of a material into another gives back exactly what went in, and is
        // often the only route to the molten form, so it survives the same category.
        Recipe(db, "r_melt", "rt~gregtech~gt.recipe.fluidextractor~LV", inputs: [("g_alu_dust", 0)],
            outputs: [(AluRod, 1, 1.0)], voltage: 30, duration: 100,
            category: "fluidExtractorRecycling");
        // A wire is half an ingot of its material, so grinding two back is honest recycling.
        Recipe(db, "r_wire_mill", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(CopperWire, 2, 1.0)]);
        Recipe(db, "r_wire_recycle", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_copper_wire_pair", 0)],
            outputs: [(CopperDust, 1, 1.0)], voltage: 4, duration: 100, category: "maceratorRecycling");
        // A cell holds its material inside a container item, so recycling it must not price.
        Recipe(db, "r_cell_recycle", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_brew_cell", 0)],
            outputs: [(CopperDust, 1, 1.0)], voltage: 4, duration: 100, category: "maceratorRecycling");
        Recipe(db, "r_brick", "t_furnace", inputs: [("g_clay_ball", 0)], outputs: [(IronIngot, 1, 1.0)]);
        Recipe(db, "r_ebf_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(EbfController, 1, 1.0)]);
        Recipe(db, "r_macerator_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MaceratorLv, 1, 1.0)]);
        Recipe(db, "r_mixer_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MixerLv, 1, 1.0)]);

        // A mixed map takes the cheaper of its single blocks and its multiblock's allowance.
        Recipe(db, "r_mixer_stack_craft", "t_shaped", inputs: [("g_copper_ingot", 0)], outputs: [(MixerStack, 1, 1.0)]);
        Recipe(db, "r_dear_stack_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(DearStack, 1, 1.0)]);
        Recipe(db, "r_mix", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_copper_ingot", 0)],
            outputs: [(MixIngot, 1, 1.0)], voltage: 480, duration: 100);
        Recipe(db, "r_dear", "rt~gregtech~gt.recipe.dearmixer~HV", inputs: [("g_copper_ingot", 0)],
            outputs: [(DearIngot, 1, 1.0)], voltage: 480, duration: 100);
        Recipe(db, "r_dry", "rt~gregtech~gt.recipe.dryer~LV", inputs: [("g_copper_dust", 0)], outputs: [(DryIngot, 1, 1.0)], voltage: 24, duration: 100);
        Recipe(db, "r_cu_hammer", "t_shaped", inputs: [("g_copper_ore", 0)], outputs: [(CopperDust, 1, 1.0)]);
        Recipe(db, "r_alu_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_alu_ore", 0)], outputs: [(AluDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_naq_ore", 0)], outputs: [(NaqDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_naq_smelt", "rt~gregtech~gt.recipe.alloysmelter~ULV", inputs: [("g_naq_dust", 0)], outputs: [(NaqIngot, 1, 1.0)], voltage: 16, duration: 100);

        // The freezer itself is naquadah-era, gating its recipes regardless of voltage.
        Recipe(db, "r_freezer_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(FreezerItem, 1, 1.0)]);
        Recipe(db, "r_freeze", "rt~gregtech~gt.recipe.vacuumfreezer~MV", inputs: [("g_alu_ingot", 0)], outputs: [(ColdIngot, 1, 1.0)], voltage: 30, duration: 100);
        Recipe(db, "r_cu_smelt", "t_furnace", inputs: [("g_copper_dust", 0)], outputs: [(CopperIngot, 1, 1.0)]);
        Recipe(db, "r_oxygen", "rt~gregtech~gt.recipe.electrolyzer~MV", inputs: [], outputs: [], voltage: 30, duration: 100, fluidInputs: [("g_water", 0)]);
        Recipe(db, "r_anneal", "rt~gregtech~gt.recipe.arcfurnace~LV", inputs: [("g_copper_ingot", 0)], outputs: [(AnnealedIngot, 1, 1.0)], voltage: 30, duration: 100, fluidInputs: [("g_oxygen", 0)]);
        Recipe(db, "r_ann_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_annealed_ingot", 0)], outputs: [(AnnealedDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_ann_smelt", "t_furnace", inputs: [("g_annealed_dust", 0)], outputs: [(AnnealedIngot, 1, 1.0)]);
        FluidOutput(db, "r_oxygen", 500, Oxygen);

        // Cell-based recipe: decomposition plus netting must leave only fluids.
        Group(db, "g_water_cell", (WaterCell, 1));
        Recipe(db, "r_electrolyze", "rt~gregtech~gt.recipe.electrolyzer~MV", inputs: [("g_water_cell", 0)], outputs: [(EmptyCell, 1, 1.0)], voltage: 30, duration: 300);
        FluidOutput(db, "r_electrolyze", 1000, Hydrogen);

        // GregTech oredicts a stone variant for every material, placed or not; this one is not,
        // so the cheap smelt is a dead end and the MV route decides the era.
        Recipe(db, "r_phantom_smelt", "t_furnace", inputs: [("g_phantom_ore", 0)],
            outputs: [(PhantomIngot, 1, 1.0)]);
        Recipe(db, "r_phantom_alt", "rt~gregtech~gt.recipe.extruder~MV", inputs: [("g_copper_ingot", 0)],
            outputs: [(PhantomIngot, 1, 1.0)], voltage: 96, duration: 200);

        // Koboldite never world-generates; its era comes from the era-only Space Mining map.
        Recipe(db, "r_miner_craft", "t_shaped", inputs: [("g_naq_ingot", 0)], outputs: [(SpaceMiner, 1, 1.0)]);
        Recipe(db, "r_space", "rt~gregtech~gt.recipe.spacemining~HV", inputs: [("g_naq_dust", 0)], outputs: [(KobOre, 1, 1.0)], voltage: 512, duration: 100);
        Recipe(db, "r_kob_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_kob_ore", 0)], outputs: [(KobDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_kob_smelt", "t_furnace", inputs: [("g_kob_dust", 0)], outputs: [(KobIngot, 1, 1.0)]);
        Recipe(db, "r_runite_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_raw_runite", 0)], outputs: [(RuniteDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_runite_smelt", "t_furnace", inputs: [("g_runite_dust", 0)], outputs: [(RuniteIngot, 1, 1.0)]);
        Recipe(db, "r_com_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_com_ore", 0)], outputs: [(ComDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_com_smelt", "t_furnace", inputs: [("g_com_dust", 0)], outputs: [(ComIngot, 1, 1.0)]);
        Recipe(db, "r_obs_use", "t_shaped", inputs: [("g_obsidian", 0)], outputs: [(Plank, 1, 1.0)]);

        // A fluid slot with alternatives at unequal amounts: 144 mB of water or 1000 mB of
        // oxygen, whichever is cheaper.
        FluidStack(db, "g_either_fluid", 144, Water);
        FluidStack(db, "g_either_fluid", 1000, Oxygen);
        Recipe(db, "r_fluid_choice", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_copper_dust", 0)],
            outputs: [(ChoiceBrick, 1, 1.0)], voltage: 512, duration: 100, fluidInputs: [("g_either_fluid", 0)]);

        // A wirelessly powered recipe: the exporter nulls the sentinel voltage, so the era
        // must come from the machine and inputs, not from the MAX label.
        Recipe(db, "r_wireless", "rt~gregtech~gt.recipe.mixer~MAX", inputs: [("g_copper_ingot", 0)],
            outputs: [(WirelessIngot, 1, 1.0)], duration: 100, label: "MAX");
        // A steam machine on the grinder map pulls its LV recipe into the steam era.
        Recipe(db, "r_grinder_craft", "t_shaped", inputs: [("g_log", 0)], outputs: [(SteamGrinder, 1, 1.0)]);
        Recipe(db, "r_steam_grind", "rt~gregtech~gt.recipe.fixturegrinder~LV", inputs: [("g_copper_ingot", 0)],
            outputs: [(SteamIngot, 1, 1.0)], voltage: 32, duration: 100);

        // A vein in both worlds, processed only through its Mars-stone block.
        Recipe(db, "r_dual_macerate", "rt~gregtech~gt.recipe.macerator~ULV", inputs: [("g_dual_ore_ma", 0)], outputs: [(DualDust, 2, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_dual_smelt", "t_furnace", inputs: [("g_dual_dust", 0)], outputs: [(DualIngot, 1, 1.0)]);

        // Inertium never bootstraps: its pile loop starves and its one real recipe eats an
        // unreachable shard. The fallback tier must come from that recipe, not the pile packing.
        Recipe(db, "r_inert_pack", "rt~gregtech~gt.recipe.packager~ULV", inputs: [("g_inert_small4", 0)], outputs: [(InertDust, 1, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_inert_split", "rt~gregtech~gt.recipe.packager~ULV", inputs: [("g_inert_dust", 0)], outputs: [(InertSmall, 4, 1.0)], voltage: 4, duration: 100);
        Recipe(db, "r_inert_real", "rt~gregtech~gt.recipe.mixer~HV", inputs: [("g_void", 0)], outputs: [(InertDust, 1, 1.0)], voltage: 512, duration: 100);
    }
}
