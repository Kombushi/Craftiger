# GTNH Crafting Planner — Specification v1.7

Target pack: **GregTech: New Horizons 2.9.0-beta2**. A web app that, for the
user's machine garage (per-machine tiers), prices every craftable item by
raw-material cost, lets the user pick targets and quantities, and renders the
total raw-material bill as a flat grid of item-icon squares.

## 1. Core features

1. **Machine garage** — a global default tier plus per-machine overrides
   (including `None` = not owned); only recipes the garage can run participate
   in pricing and planning. The garage UI shows only machines relevant to the
   current cart (§7).
2. **Sorted craft list** — items sorted by computed material cost, cheapest first.
   Items with no recipe the garage can run (cost `∞`) render grayed at the
   bottom; a "hide unreachable" toggle removes them.
3. **Full breakdown to leaves** — every plan resolves down to leaf materials
   (ingots, dusts, gems, logs, minable blocks, free fluids).
4. **Quantity input** — per-target craft count; multiple targets merge into one bill.
5. **Recipe pinning** — per item, the user may pin a producing recipe that
   overrides the auto-cheapest pick.
6. **Immediate + raw result** — for every target, the result shows the chosen
   recipe's direct inputs, alongside the merged raw-material totals for the
   whole cart.

## 2. Definitions

- **Tier ladder**: `Steam = 0, LV = 1, MV = 2, HV = 3, EV = 4, IV = 5, LuV = 6,
  ZPM = 7, UV = 8, …` — extended upward to whatever the recipe dump contains.
- **Voltage tier** of a recipe: GT's own per-recipe tier label from the dump
  (`ULV`/`LV` → 1, `MV` → 2, `HV` → 3, …), which already accounts for
  machine amperage quirks (3-amp arc furnaces, 2-amp thermal centrifuges).
  Fallback when the label is absent: smallest `n ≥ 1` with
  `EU/t ≤ 32 × 4^(n−1)`. Multiblocks are fed by two 2-amp energy hatches and
  run recipes one tier above the hatches, so their recipes tier one step
  lower — an LV-hatched EBF legally runs 120 EU/t recipes. The builder
  detects multiblock maps from the dump: a single-block map lists its tiered
  machine family as NEI handlers (9+), a multiblock map lists its few
  controllers; per-machine overrides are builder config. Zero-EU/t recipes,
  and those run by steam machines, a plain furnace, or a crafting table, map
  to tier 0.
- **Machine**: a recipe map (NEI recipe category), not a specific block.
  Multiblocks are machines like any other (Pyrolyse Oven, Distillation Tower,
  Large Chemical Reactor, Implosion Compressor, Vacuum Freezer, Assembly
  Line, …); a map shared by several blocks (furnace / Multi Smelter,
  macerator / maceration stack) is one garage row.
- **Garage**: the machines the user owns. A global default tier plus
  per-machine overrides; `effectiveTier(machine) = override ?? globalDefault`,
  and `None` marks a machine as not owned. For multiblocks the tier means the
  best energy hatch installed. The EBF is configured by two values: voltage
  tier and installed coil — it is the only heat-gated map. Crafting table and
  furnace are always owned at tier 0.
- **Garage-legal recipe**: `recipe.tier ≤ effectiveTier(recipe.machine)`; EBF
  recipes additionally require `recipe.heat ≤ maxHeat(installed coil)`.
  Recipes of `None` machines are never legal.
- **Upstream closure** of a set of items: every recipe (and its machine) that
  could take part in any production route of those items, found by walking
  "producible-by" edges tier-agnostically from the targets down to leaves.
- **Leaf**: an item the planner never expands; it carries a weight (its cost).
- **Cost** of an item: cheapest way to obtain one unit from leaves, using only
  garage-legal recipes (§4–5).
- **BOM**: the flat `item → amount` bill of leaf materials for the cart (§6).
- **Pin**: a user-chosen producing recipe for an item, stored client-side.

## 3. Data pipeline (offline build)

**All modpack parsing happens offline in the builder; the app consumes only the
three build artifacts.**

Stage 1 — export. Run the NESQL Exporter mod (`/nesql` command) inside a client
instance of the exact target pack version; the dump lands in `.minecraft/nesql`.
Caveats:

- Thaumcraft data exports only for research the exporting character knows — use a
  save with the full TC tree unlocked (or creative-cheated).
- Open the NEI item list once before exporting so all items load.
- The dump is an HSQLDB database (Java-only format), roughly 600 MB for GTNH;
  the export can take on the order of an hour.
- The dump must be re-generated for every pack update; the builder records the
  pack version into `meta`.

Stage 2 — build. The builder (`Gtnh.Planner.Builder`, a standalone console
project — repo layout in §8) first converts the HSQLDB dump into a local
`dump.sqlite` copy over JDBC (the only step requiring a JRE — .NET has no
HSQLDB driver), then produces:

- `planner.sqlite` — slim relational data (schema below)
- `atlas.webp` — one texture atlas of all item/fluid icons
- `atlas-offsets.json` — `itemId → (u, v)` pixel offsets into the atlas

These artifacts are the only contract between the builder and the API.
`dump.sqlite` is an intermediate — a faithful SQLite copy of the dump kept
locally for builder runs and ad-hoc queries; it is never shipped.

Builder responsibilities, in order:

1. **Unification** — collapse oredict-equivalent items into one canonical item;
   keep an alias table for search. Wildcard grouping oredicts (`ingotAnyIron`,
   `listAll*`, `crafting*` — editable pattern list) are accept-lists over
   distinct materials, not equivalence classes, and never drive unification.
2. **Normalization** — decompose every filled container (cell, bucket) into
   empty container + fluid, then net out items appearing on both sides of one
   recipe. Balanced containers vanish, so cell-only recipes become their
   fluid form automatically; unmatched containers survive as real inputs or
   outputs and stay priced. Then strip non-consumed inputs: the dump marks
   most catalysts (programmed circuits, molds, shapes, lenses) with stack
   size 0, which is the primary signal; a static editable prefix list
   additionally strips GT crafting tools (wire cutter, hammer, file, saw,
   screwdriver, wrench, …), which crafting-grid recipes list at size 1.
3. **Exclusion** — drop every recipe source listed under "Excluded by design"
   (§9).
4. **Tier tagging** — per recipe: voltage tier per §2 (GT label, hatch
   allowance for multi-amp multiblocks). Machine names
   normalize by stripping the recipe map's constant
   voltage suffix ("Macerator (ULV)" → "Macerator") and merging crafting
   variants (shaped/shapeless → "Crafting Table"). Any recipe with a coil
   heat requirement keeps it in `recipes.heat` (EBF and its multiblock
   upgrades); the coil list (name → max heat + tier equivalent, builder
   config) is exported into `meta` for the garage UI.
5. **Leaf tagging** — mark leaves by oredict prefix and lists (§4).
6. **Ingot tiering** — an ingot's tier is its production era, computed as a
   min-of-max fixpoint over the whole recipe graph:
   `era(item) = min over producing recipes of max(intrinsic recipe tier,
   era of every input)`. Era seeds at 0 are world-origin items only: minable
   blocks, farmables, logs, gems, free fluids, and mined `ore*` items —
   except ores that spawn only in later-dimension worlds, which seed at that
   dimension's tier (Moon = HV, Mars = EV, …; material → era map in builder
   config). Dusts
   are deliberately not seeded — a dust obtainable only by macerating its own
   metal (annealed copper) inherits the metal's era, while ore-processing
   dusts still reach era 0 through tier-0 crushing. An EBF
   recipe's intrinsic tier is `max(voltage tier, coil tier of its required
   heat)`. Machine availability gates the era too: each recipe's intrinsic
   tier includes the era of the cheapest producible machine handling its map
   (NEI handler items), so a chain through a Large Chemical Reactor cannot
   land below the era of building one. Cleanroom-flagged recipes additionally
   inherit the Cleanroom Controller's era, which is pinned at HV — the pack's
   circuit-line progression wall, a fact the recipe graph cannot derive.
   Steam-handled maps run their LV-and-below recipes in the steam era. A
   naive per-recipe minimum would be poisoned by recycling
   (plate → ingot smelting, arc-furnacing machines, block ↔ ingot cycles);
   in the fixpoint those routes need the ingot's own era first and starve.
   Era is independent of the garage: bronze and steel land at 0 via
   furnace / bricked blast furnace, aluminium at its EBF tier. Ingots with
   no bootstrappable route fall back to the cheapest direct recipe tier.

### Slim schema (`planner.sqlite`)

- `items(id, name_en, oredict, is_fluid, leaf_class NULL, atlas_idx)`
- `item_aliases(item_id, alias)` — merged names and oredict names for search
- `recipes(id, machine, tier, heat NULL, duration_ticks, eu_t)` — `heat` for
  coil-gated recipes only
- `recipe_inputs(recipe_id, item_id, amount)` — amount in units, or mB for fluids
- `recipe_outputs(recipe_id, item_id, amount, chance)` — `chance ∈ (0, 1]`
- `item_tiers(item_id, tier)` — ingots only
- `meta(key, value)` — pack version, dump date, atlas dimensions, coil list

## 4. Cost model

**Material cost only** (exclusions: §9).

### Leaves and weights

| Leaf class | Membership rule | Default weight |
|---|---|---|
| Minable block | explicit list: stone, cobblestone, sand, gravel, dirt, netherrack, … | 1 |
| Ingot | oredict `ingot*` | `B × 4^tier` (see below) |
| Dust | oredict `dust*` | 1 |
| Small / tiny dust | `dustSmall*` / `dustTiny*` | parent dust ÷ 4 / ÷ 9 |
| Gem | oredict `gem*` | 1 |
| Log | oredict `logWood` | 1 |
| Farmable | explicit list: sugar cane, seeds, saplings, crops, … | 1 |
| Free fluid | explicit list, default: water | 0 |

All rules and weights live in **one editable weights table** (config UI, §7). The
defaults are deliberately crude (a diamond prices equal to a redstone dust); the
table is the tuning surface.

### Ingot pricing

`cost(ingot) = B × 4^tierIndex`, with `tierIndex` from §3 step 6 and **B = 4** by
default, exposed as a config slider. Bronze (Steam, 4⁰) = 4; an LV-tier ingot = 16;
each tier multiplies by 4, mirroring EU voltage steps.

### Recipe candidate cost

For a garage-legal recipe and one chosen output:

```
candidate(output) = Σ over inputs (cost(input) × amount) / (output_amount × chance)
```

- **Chanced outputs use expected value** — dividing by `chance` prices the average
  number of runs needed. It also keeps otherwise-chance-only items reachable.
- **Each output is priced independently** from the full input cost of its
  recipe (byproduct exclusion: §9).
- **Fluid costs are per mB** — a fluid's `cost` is per millibucket and recipe
  amounts are in mB, so the formula needs no special casing. Steam consumed
  as power is energy, not an ingredient (§9).

## 5. Cost engine

**Costs are solved by a strict-improvement worklist fixpoint; the resulting
`bestRecipe` pointers always form a DAG, so recipe cycles need no special casing.**

Mechanism:

1. Initialize `cost[leaf] = weight`, all other items `+∞`.
2. Queue every garage-legal recipe. Pop a recipe; if any input is `∞`, skip.
3. For each output, compute the candidate (§4). Update `cost` and `bestRecipe`
   only on strict improvement: `candidate < cost[item] − ε`, `ε = 1e-9`.
4. On update, re-queue every recipe consuming the improved item. Stop at empty queue.

Why this shape:

- A cycle can never strictly undercut itself (ingot → block → ingot re-offers the
  same price), so loops starve instead of oscillating, and `bestRecipe` stays acyclic.
- Costs only decrease and are bounded below by 0, so termination is guaranteed.
- Full-pack scale (a few hundred thousand recipes) converges in well under a
  second in memory.

Caching and pins:

- Solved cost tables are cached keyed by `(garageHash, B, weightsHash)`, where
  `garageHash` covers the default tier, all overrides, and the EBF coil.
- **Pins never enter the cache key.** The sorted list always shows the unpinned
  baseline; pins are applied as an overlay when resolving the item detail view and
  the BOM walk. v1 simplification: a pin changes recipe *choice*, not the listed price.
- A pin whose recipe the garage cannot run is ignored with a visible red
  warning, falling back to auto-cheapest. Pins cannot bypass the garage filter.

## 6. BOM computation

**Totals are computed on the `bestRecipe` DAG (pins overlaid); the rendered grid
is a projection of that result.**

1. Seed `demand[target] += count` for every cart entry — multiple targets merge
   automatically.
2. Walk items in reverse topological order of the DAG. For a non-leaf item:
   `runs = demand / (output_amount × chance)` of its chosen recipe, then add
   `runs × amount` to each input's demand.
3. Leaves accumulate into the final `item → amount` map; fluid amounts stay in mB.

Output per request: per-target direct inputs (chosen recipe, `runs × amount`
per input), merged leaf totals, and warnings (ignored pins, unreachable targets).

## 7. UI

Single-page app, English item names (dump locale). Screens:

- **Search** — type-ahead over canonical names and oredict aliases; results show
  icon, name, current cost.
- **Craft list** — cost-ascending; grayed `∞` section at bottom; "hide
  unreachable" toggle; tapping opens item detail.
- **Item detail** — all garage-legal producing recipes with their candidate
  costs, current pick highlighted, pin/unpin button per recipe.
- **Cart** — targets with count inputs; "compute" produces the result grid.
- **Result** — two sections built from the same square component (CSS offsets
  into `atlas.webp`, count badge per square, `1.2k`-style formatting; fluids
  render as their cell/bucket icon with an mB badge):
  1. *Immediate* — one row per cart target: the target square with its count,
     then the direct inputs of its chosen recipe, scaled by runs.
  2. *Raw materials* — the merged leaf totals for the whole cart.
  Tapping any square opens that item's detail.
- **Garage** — global default tier (Steam…max) plus a machine list
  **filtered to relevance**: only machines in the current cart's upstream
  closure (§2) get a picker; a "show all machines" toggle reveals every machine
  that has recipes. Hidden machines inherit the global default. With an empty
  cart, the list starts empty apart from the toggle. One picker per shown
  machine (`inherit / None / Steam / LV / …`); the EBF row has a voltage picker
  and a coil dropdown; crafting table and furnace are shown as always-owned.
- **Config** — `B` slider, editable leaf-weights table, minable-block and
  free-fluid lists.

## 8. Architecture

### Repository layout

- `Gtnh.Planner.Builder/` — standalone .NET console project; NESQL dump in,
  artifacts out. Runs offline on demand, is never deployed, and holds no
  project reference to or from the API — the artifacts (§3) are the contract.
- `Gtnh.Planner.Solver/` — pure class library: the cost engine (§5) and BOM
  computation (§6). No I/O and no dump dependency; referenced by the API and
  exercised directly by fixture tests.
- `Gtnh.Planner.Api/` — .NET minimal API.
- `Gtnh.Planner.Tests/` — single xUnit project covering Builder, Solver, and
  API.
- `web/` — React SPA.

### Runtime

- **Backend**: the minimal API, stateless, deployed on the `ryokutek` k8s
  cluster. Loads `planner.sqlite` read-only; holds the solver and its cost-table
  cache in memory.
- **Frontend**: React SPA served statically alongside `atlas.webp` and
  `atlas-offsets.json`.
- **Client state**: everything user-specific lives in browser `localStorage` and
  travels with each request — the API stores nothing per user.

### Endpoints

- `POST /api/solve` — body `{garage, b, weights}` → `{solveId}`; runs or reuses
  a cached cost solve. A `404` on any later call means the cache entry was
  evicted — the client re-posts.
- `GET /api/search?q=&solveId=` → `[{itemId, name, atlasIdx, cost}]`
- `GET /api/list?solveId=&page=&hideUnreachable=` → cost-sorted page
- `GET /api/item/{id}?solveId=` → producing recipes with candidate costs
- `GET /api/machines?targets=` — upstream-closure machine list for the given
  item ids; drives the relevance-filtered garage.
- `POST /api/bom` — body `{solveId, targets: [{itemId, count}],
  pins: {itemId: recipeId}}` → `{targets: [{itemId, count, recipeId,
  inputs: [{itemId, amount}]}], leaves: [{itemId, amount}], warnings}`
- `GET /api/meta` → tier ladder, machine list, coil list, pack version, atlas
  dimensions
- Static: `/atlas.webp`, `/atlas-offsets.json`

### localStorage keys

`gtnhp.cart`, `gtnhp.pins`, `gtnhp.weights`, `gtnhp.machines` (default tier,
overrides, EBF coil), `gtnhp.config` (B), `gtnhp.ui`.

## 9. Exclusions, non-goals, and risks

All "does not / never" rules live here; other sections only reference this one.

### Excluded by design

- **Crafting-tree and step-list views** — the result is always the flat BOM grid.
- **Energy and machine time in prices** — cost is material-only.
- **Byproduct credit** — sibling outputs never reduce a recipe's cost;
  crediting them collapses all prices toward zero through recycling loops.
- **Pseudo-recipe sources** — bee breeding, mob drops, dungeon/chest loot,
  GT informational tabs (fuel values, material lists), and mining maps that
  output ore blocks from equipment (Space Mining); the builder drops them
  (§3 step 3) because they conjure matter from nothing and poison prices —
  an ore-from-drill recipe forms an amplifying cycle that spirals every
  cost to zero.
- **Overclocking, parallelism, and multiblock efficiency bonuses** — they
  affect time, energy, or machine-specific discounts; recipes price at their
  listed amounts.
- **Recipe unlock prerequisites** — Assembly Line data sticks, TC research and
  similar gates are not modeled; owning the machine at a tier makes its
  recipes available.

### Deferred to v2+

- Ukrainian locale; server-side profiles; settings export/import;
  pseudo-recipe sources shown as read-only "alt sources"; revisiting byproduct
  credit.

### Risks

- Clearing browser data erases pins, weights, garage, and cart — accepted;
  mitigations (export/import, server profiles) are v2.
- Non-consumed-input detection is a heuristic prefix list; misses inflate costs
  slightly until the list is amended.
- Dump completeness depends on the export save (TC research unlocked, NEI item
  list loaded).

## 10. Acceptance checks

1. Raising the global default or any single machine's tier never increases any
   item's cost.
2. Bronze ingot prices at `B`; each ingot tier step multiplies by 4.
3. A 90%-chance sole output requires ~1.11× the inputs of its guaranteed twin.
4. Two cart targets sharing an intermediate produce one merged leaf total.
5. A pinned recipe changes the BOM; pinning one the garage cannot run shows a
   warning and changes nothing.
6. Sorted list shows `∞` items grayed at the bottom until hidden by the toggle.
7. An extruder-only recipe is unreachable while the extruder is `None`,
   regardless of other machines' tiers.
8. An EBF recipe whose heat exceeds the installed coil stays unreachable even
   at sufficient voltage.
9. The garage lists only machines from the cart's upstream closure by default;
   "show all machines" reveals every machine that has recipes.
10. For a target whose recipe includes copper cable, the result shows the cable
    under immediate inputs and its constituent copper ingots inside the raw
    totals.
11. A cell-based recipe variant and its direct-fluid twin produce identical
    costs after container normalization.
12. An empty cell consumed as a genuine component (no matching output) stays
    in the raw totals; balanced cells never appear in them.
