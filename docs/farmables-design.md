# Pricing vanilla and Pam's farmables

Options for the `farmable` leaves the farms slice left unpriced, recorded
for a later slice. Everything here extends `docs/pipelines-design.md` §4.6;
schema and scope rules from spec §3 and §9 apply unchanged.

## The residue

The `farmable` leaf class holds 616 items, none with weight overrides, so
every one prices at the default leaf weight 1 wherever no farm line exists.
The split, by name and mod:

- **200 saplings and ~330 tree products** — Forestry (272) and ExtraTrees
  (55) species saplings, leaves and fruit (Pomelo, Silver Lime, …), plus
  BiomesOPlenty, Natura, Twilight Forest and Pam's fruit. The dominant
  problem is trees.
- **~30 ground-crop seed items** (Barley, Beans, …) and their crops —
  vanilla-style `BlockCrops` plants outside CropsNH.

The Tree Growth Simulator map does not cover these saplings; CropsNH's
Crop Manager and Industrial Farm only serve CropsNH crops.

## Options

### Exporter tables (preferred: the dump stays the only fact source)

1. **EIG normal-mode table.** A plugin feeds every plantable seed through
   the EIG's own bucket code (`EIGSeedBucket`, `EIGStemBucket`,
   `EIGFlowerBucket`) in-game and exports expected drops per cycle — the
   CROPS_NH plugin pattern. The builder synthesizes EIG rows like the
   farms slice did; no solver change. Covers the ground crops, melons,
   pumpkins and flowers, from EV up. Small on both sides.

2. **Vanilla-growth table.** Export per-crop stage counts and growth
   modifiers for `BlockCrops` plants; average maturation over random ticks
   is a closed-form vanilla mechanic the builder can code. Unlocks
   early-game lines on the EnderIO Farming Station (its power-per-action
   needs one source pin) or plain manual-farm rows. Only worth it if
   pre-EV ground-crop automation matters in practice — the Crop Manager
   already covers the CropsNH equivalents from LV.

3. **Tree-yield table.** Per sapling, expected logs, fruit and sapling
   returns. Forestry's API answers fruit family, yields and sappiness per
   species in-game; log counts come from tree generation, which the API
   does not hand out — likely an in-game test-grow, else a curated
   per-species log estimate. Unlocks Forestry multifarm (arboretum and
   orchard) lines — the only honest machine for the tree items, and the
   largest research chunk. Wants its own design pass on Forestry farm
   mechanics (operation cadence, fertilizer and water draw, farm block
   tiering).

### Builder only

4. **Honest weights instead of lines.** Farmables stay purchases but stop
   costing a flat 1: derive weights from the era of the item's era-only
   harvest recipe (the ladder gems and dusts already use), or a configured
   per-class weight. One small service, immediate effect on both tabs, no
   guessed facts. The recommended stopgap regardless of the machine work.

5. **Curated rates.** Per-item or per-class drops-per-second on a generic
   farm machine, flagged Estimated. Hand-guessing; acceptable only as a
   bounded stopgap with a conservative class-wide default.

### Solver only

Nothing real: the solver cannot conjure rates no data source carries. Its
contribution stays presentational (the Estimated flag).

## Recommendation

Two tracks: ship option 4 now as the cheap correctness fix, then an
exporter round for options 1 and 3 — option 1 is small, option 3 is a real
slice with its own design pass. Option 2 only on demand.

A structural note that keeps option 3 honest: farm lines take the sapling
as a non-consumed catalyst, and catalysts neither price nor gate — so tree
lines work even though Forestry species saplings come from breeding
(excluded by spec §9) and stay leaf-priced themselves.
