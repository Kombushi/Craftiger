# GTNH Crafting Planner — Specification v1.8

Target pack: **GregTech: New Horizons 2.9.0-beta-2**. A web app that, for the
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
   (ingots, gems, dusts and their fractions, nuggets, logs, minable blocks,
   world fluids, crop drops).
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
  run recipes one tier above the hatches, so a recipe run on one tiers a step
  lower — an LV-hatched EBF legally runs 120 EU/t recipes. The allowance is a
  property of the machine, not the map: the dump names every machine serving a
  map and whether it is a multiblock, and many maps (macerator, assembler,
  centrifuge) are served by both kinds. A recipe therefore has a tier per
  machine, and the era fixpoint takes the cheapest machine that can actually
  run it (§3 step 6), so the allowance only applies once the multiblock itself
  is affordable. Zero-EU/t recipes, and those run by steam machines, a plain
  furnace, or a crafting table, map to tier 0.
- **Machine**: a recipe map (NEI recipe category), not a specific block.
  Multiblocks are machines like any other (Pyrolyse Oven, Distillation Tower,
  Large Chemical Reactor, Implosion Compressor, Vacuum Freezer, Assembly
  Line, …); a map shared by several blocks (furnace / Multi Smelter,
  macerator / maceration stack) is one garage row.
- **Garage**: the machines the user owns. A global default tier plus
  per-machine overrides; `effectiveTier(machine) = override ?? globalDefault`,
  and `None` marks a machine as not owned. For multiblocks the tier means the
  best energy hatch installed. A map served by both kinds of machine carries a
  second switch, whether its multiblock is built, because the hatch allowance
  is worth a tier and belongs to whoever built the multiblock. The EBF is
  configured by two values: voltage tier and installed coil — it is the only
  heat-gated map. Crafting table and furnace are always owned at tier 0.
- **Garage-legal recipe**: `required ≤ effectiveTier(recipe.machine)`, where
  `required` is `recipe.tier`, or `recipe.multi_tier` when the map has one and
  the garage says the multiblock is built. EBF recipes additionally require
  `recipe.heat ≤ maxHeat(installed coil)`. Recipes of `None` machines are
  never legal.
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

Stage 2 — build. The builder (`Craftiger.Builder`, a standalone console
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
   screwdriver, wrench, …), which crafting-grid recipes list at size 1. One
   catalyst condemns its whole slot: a tool slot lists every mod's version of
   that tool and the prefix list only recognises GregTech's, so judging members
   one at a time would leave the third-party tools priced as ingredients.
3. **Exclusion** — drop every recipe source listed under "Excluded by design"
   (§9).
4. **Tier tagging** — per recipe: voltage tier per §2 (GT label). Two tiers
   ship, because 41 of the pack's 167 maps are served by both single-blocks and
   a multiblock, and between them they carry 85% of GT's recipes: `tier` is what
   a single-block needs, and `multi_tier` what the multiblock needs, set only
   where owning one actually lowers the bar. A map with nothing but multiblocks
   has no second option, so its `tier` already carries the allowance and its
   `multi_tier` stays empty. Machine names normalize by stripping the recipe
   map's constant voltage suffix ("Macerator (ULV)" → "Macerator") and merging
   crafting variants (shaped/shapeless → "Crafting Table"). Any recipe with a coil
   heat requirement keeps it in `recipes.heat` (EBF and its multiblock
   upgrades); the coil list (name → max heat + tier equivalent, builder
   config) is exported into `meta` for the garage UI. Macerator byproduct
   slots only exist on tiered machines (2nd slot HV, 3rd EV, 4th IV; builder
   config): each maceration recipe splits into a primary-only variant at the
   map's tier plus cumulative variants (`id~b3`, `id~b4`, …) floored at the
   slot's tier, so byproducts stay behind the right garage tier and era, and
   steam macerators grind primaries only.
5. **Leaf tagging** — mark leaves by oredict prefix and lists (§4).
   Ore-washing and blast-furnace intermediates (`crushed*`, `dustImpure*`,
   `dustPure*`, `ingotHot*`) are never leaves: they exist only inside a chain,
   so a flat weight on one would cap every material made through it. What a
   CropsNH crop drops is a leaf too, but only where farming is the one way in —
   anything another recipe also makes is priced from that recipe. Crop drops
   are claimed last so a vanilla farmable oredict always wins, and by item id
   since most carry no oredict. Once eras are known the leaf set is pruned
   again: a tiered leaf the fixpoint never reached, and a fraction whose parent
   is not itself priced, lose their class and fall back to their recipes, so
   every leaf that ships has a weight the solver can work out. The
   minable-block list names blocks by oredict or, where the dump gives a block
   none at all (clay), by item id, and matches every oredict of the unified
   item, not only its primary, since unification prefers `block*` names
   (`blockObsidian` would otherwise hide `obsidian`). This list stays builder
   config because the dump cannot supply it: it names the dimension each stone
   type belongs to, but nothing ties a stone type to the block item it places,
   and no `stone<Dimension>` oredicts exist. A block left off the list gets no
   era at all, so off-world stone stays unreachable rather than free.
6. **Ingot tiering** — an ingot's tier is its production era, computed as a
   min-of-max fixpoint over the whole recipe graph:
   `era(item) = min over producing recipes of max(intrinsic recipe tier,
   era of every input)`. World-origin items seed the fixpoint: farmables,
   logs, minable blocks, the world fluids config lists (water, lava), and
   mined `ore*` items —
   except ores generated only in later worlds, which seed at the era of
   reaching their cheapest generating dimension. That era is derived from the
   dump's GT worldgen tables (veins, small ores, dimension tiers) through two
   builder-config maps: a dimension-tier → era ladder (T1 rocket = HV,
   T2 = EV, …) and per-dimension eras for tier-0 worlds reached without a
   rocket (Nether = Steam, End = HV, Everglades = ZPM, …). Veins disabled in the
   default worldgen config are ignored, and a dimension in neither map
   contributes nothing. GT also oredicts every stone variant of an ore
   (`oreSethIceCallistoIce`), including variants no vein places; those resolve
   to their material by longest name suffix, so `MeteoricIron` wins over
   `Iron`. Ore blocks that exist as items but never world-generate (a
   builder-config list: the GT++ leftovers plus the Space Mining ores) get no
   seed at all — their era comes from recipes, or for Space Mining ores from
   the era-only mining maps (§9). Mined small-ore drops and `rawOre*` chunks
   (dropped by mining GT++ ore blocks) also start at their dimension era, but
   as soft seeds that recipes may still lower — mining is one more route, not
   a floor. Dusts
   are deliberately not seeded — a dust obtainable only by macerating its own
   metal (annealed copper) inherits the metal's era, while ore-processing
   dusts still reach era 0 through tier-0 crushing. Gems are not seeded
   either: like ingots they earn an era from the recipes that cut or grow
   them, so a diamond from a tier-0 ore stays cheap while an endgame crystal
   does not. Dusts are tiered the same way, by their own era, which the
   matching `ingot*` or `gem*` twin then overrides where one exists — a dust
   is the same material as its metal. An EBF
   recipe's intrinsic tier is `max(voltage tier, coil tier of its required
   heat)`. Machine availability gates the era too: a recipe costs what the
   cheapest machine that can run it costs, taking for each machine serving its
   map the highest of the machine's own era, the machine's input-voltage tier,
   and the recipe's tier on that machine (§2, hatch allowance for multiblocks).
   A machine buildable early still waits for its power tier, so an MV-only
   dehydrator cannot run in the LV era; and a multiblock's tier allowance
   costs whatever the multiblock costs, so a recipe only gets it once that
   machine is affordable. Machine voltage comes from the recipe map's machine
   tier, or from the "Voltage IN" tooltip for machines the map does not tier
   (multiblock controllers). So a chain through a Large Chemical Reactor cannot
   land below the era of building one. Cleanroom-flagged recipes additionally
   inherit the Cleanroom Controller's era, which is pinned at HV — the pack's
   circuit-line progression wall, a fact the recipe graph cannot derive.
   Steam-handled maps run their LV-and-below recipes in the steam era. Crop
   drops are not seeded: each non-hidden crop gets an era-only harvest recipe
   (§9) taking its seed and, when it needs one, the cheapest block it grows
   on — so an Aluminium Oreberry dates from the Moon rather than era 0. Every
   fluid the dump says is pumpable gets an era-only pumping recipe (§9) at its
   cheapest dimension's era, gated by the cheapest drilling rig that can be
   built — oil lies in the Overworld but still waits for the rig. Pumping only
   ever gates: being pumpable does not make a fluid free, so hydrogen and the
   other gases with underground deposits still price through their chemistry,
   and only the world-fluid list (§4) is a leaf. A
   naive per-recipe minimum would be poisoned by recycling
   (plate → ingot smelting, arc-furnacing machines, block ↔ ingot cycles);
   in the fixpoint those routes need the ingot's own era first and starve.
   Era is independent of the garage: bronze and steel land at 0 via
   furnace / bricked blast furnace, aluminium at its EBF tier. Ingots with
   no bootstrappable route fall back to the cheapest direct recipe tier.

7. **Price check** — the builder prices its own output once, at the default
   weights, and reports every leaf that comes out below a millionth of its own
   weight. A leaf weight is only the price when no route exists, so a cheap
   route beating it is ordinary; beating it by orders of magnitude is a recipe
   loop handing back more material than it consumed, and every price downstream
   of one is fiction. The counts land in `meta` as `price_leaks`,
   `price_free_items` and `price_converged`, so the artifacts carry their own
   verdict. This is a build-time sanity check, not the shipped cost engine (§5),
   which prices against the user's own garage and weights.

### Slim schema (`planner.sqlite`)

- `items(id, name_en, oredict, is_fluid, leaf_class NULL, atlas_idx)`
- `item_aliases(item_id, alias)` — merged names and oredict names for search
- `recipes(id, machine, tier, multi_tier NULL, heat NULL, duration_ticks, eu_t)`
  — `multi_tier` for maps whose multiblock lowers the tier, `heat` for
  coil-gated recipes only
- `recipe_inputs(recipe_id, item_id, amount, slot)` — amount in units, or mB for
  fluids; rows sharing a `slot` are alternatives the recipe accepts any one of
- `recipe_outputs(recipe_id, item_id, amount, chance)` — `chance ∈ (0, 1]`
- `item_tiers(item_id, tier)` — tiered materials: ingots, gems and dusts (§4)
- `item_weights(item_id, weight)` — weights overriding the item's leaf class,
  where one class covers items worth different amounts (§4)
- `meta(key, value)` — pack version, dump date, atlas dimensions, coil list, and
  the price check's verdict (§3 step 7)

## 4. Cost model

**Material cost only** (exclusions: §9).

### Leaves and weights

| Leaf class | Membership rule | Default weight |
|---|---|---|
| Minable block | explicit list, each with the era of the cheapest world it is mined in (End Stone at HV) | 1 |
| Ingot | oredict `ingot*` | `B × 4^tier` (see below) |
| Dust | oredict `dust*` | `B × 4^tier`, from the matching `ingot*` or `gem*` where the material has one, else from the dust's own era |
| Small / tiny dust | `dustSmall*` / `dustTiny*` | parent dust ÷ 4 / ÷ 9 |
| Nugget | oredict `nugget*` | parent ingot or gem ÷ 9 |
| Gem | oredict `gem*` | `B × 4^tier`, tiered like an ingot |
| Log | oredict `logWood` | 1 |
| Farmable | explicit list: sugar cane, seeds, saplings, crops, … | 1 |
| Crop drop | what a CropsNH crop drops, where no other class claims it | 1 |
| World fluid | explicit list: water, lava, oil and its cuts, natural gas | per fluid, from `item_weights`: water 1, lava 2, oil and gas 8 |

Ore-washing and blast-furnace intermediates (`crushed*`, `dustImpure*`,
`dustPure*`, `ingotHot*`) are **not** leaves in any class — see §9.

All rules and weights live in **one editable weights table** (config UI, §7).
An `item_weights` row overrides its item's class, for a class whose members are
not worth the same. The defaults are deliberately crude; the table is the
tuning surface.

### Ingot pricing

`cost(ingot) = B × 4^tierIndex`, with `tierIndex` from §3 step 6 and **B = 4** by
default, exposed as a config slider. Bronze (Steam, 4⁰) = 4; an LV-tier ingot = 16;
each tier multiplies by 4, mirroring EU voltage steps.

### Recipe candidate cost

For a garage-legal recipe and one chosen output:

```
candidate(output) = Σ over slots (min over alternatives (cost × amount))
                    / (output_amount × chance)
```

- **A slot with alternatives costs its cheapest one.** Some 3,600 recipes accept
  any of several items in a slot — `listAllmeatraw`, `ingotAnyIron` — and which
  one is cheapest depends on the garage and the weights table, so it cannot be
  decided at build time.
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
  world-fluid lists.

## 8. Architecture

### Repository layout

- `src/Craftiger.Builder/` — standalone .NET console project; NESQL dump in,
  artifacts out. Runs offline on demand, is never deployed, and holds no
  project reference to or from the API — the artifacts (§3) are the contract.
  Paths, pack version and every builder-config list live in `appsettings.json`,
  bound through `IOptions`; the tests run against that same file.
- `src/Craftiger.Solver/` — pure class library: the cost engine (§5) and BOM
  computation (§6). No I/O and no dump dependency; referenced by the API and
  exercised directly by fixture tests.
- `src/Craftiger.Api/` — .NET minimal API.
- `tests/Craftiger.Builder.UnitTests/` — xUnit tests for the Builder;
  Solver and API tests get sibling projects under `tests/`.
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
  and GT informational tabs (fuel values, material lists); the builder drops
  them (§3 step 3) because they conjure matter from nothing and poison
  prices — an ore-from-drill recipe forms an amplifying cycle that spirals
  every cost to zero. Mining maps that output ore blocks from real equipment
  (Space Mining) are *era-only*: they gate progression in the era fixpoint
  (§3 step 6) but never reach `planner.sqlite`, so they can never price. The
  same holds for pumping a fluid out of the ground: the rig gates when the
  fluid becomes available, but the fluid itself prices as a world fluid.
  Harvesting a CropsNH crop is era-only for the same reason: growing a crop is
  renewable, so its drops price as leaves, while the harvest edge still dates
  them by what the crop needs — a seed, and one of the blocks it grows on.
  Drop weights and chances are deliberately ignored, since era-only recipes
  never price.
  Breaking a block is the one exception that does price. The dump records what
  each block drops without silk touch or fortune, and the builder turns those
  into ordinary recipes on the always-owned `Mining` machine — a clay ball
  costs a quarter of a clay block. This conjures nothing: the block is
  consumed, and a block no route can reach simply never prices. Drops equal to
  the block itself are not recipes at all, just picking the block back up.
- **Recycling** — GregTech files reverse-crafting under its own recipe
  categories (`arcFurnaceRecycling`, `maceratorRecycling`,
  `fluidExtractorRecycling`, `alloySmelterRecycling`, `forgeHammerRecycling`),
  and every category whose name ends in `recycling` is dropped. Melting a
  crafted item down is not how it is made, and GregTech decides what comes back
  from the item's material composition rather than from the recipe that built
  it — so wherever the pack also sells a cheaper crafting recipe, the round trip
  returns more than it consumed. An iron door costs four plates and arc-furnaces
  into six ingots; left in, that loop alone drives iron, and everything built
  from iron, toward zero. Storage-block cycles are safe by contrast: nine ingots
  in, nine ingots back.
- **Matter fabrication** — the Replicator, Matter Fabricator, Matter Amplifier
  and Mass Fabrication turn scrap and EU into any element. Their real price is
  energy, which this model refuses to count, so it can only ever undercount
  them: replicated neutronium comes out at roughly half its own leaf weight,
  and everything neutronium follows it down. Excluding them is consistency
  with the material-only cost model, not a workaround. The GT Recycler is
  excluded alongside them — it eats almost any item for scrap by chance, and
  exports no recipes to price in the first place.
- **Intermediates as leaves** — an item that exists only partway through a
  chain (`crushed*`, `dustImpure*`, `dustPure*`, `ingotHot*`) never carries a
  leaf class, whatever its oredict prefix says. A leaf weight is a ceiling the
  solver may undercut but never exceed, so pricing a purified pile at a flat
  weight would cap the metal it becomes, and everything built from it. The
  same reasoning bars a crop drop another recipe already makes, and any leaf
  whose weight cannot be worked out (§3 step 5).
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
13. A clay ball costs a quarter of a clay block, from breaking the block (§9).
14. A nugget costs a ninth of its ingot, and no ingot prices from a nugget.
15. No ore-washing or blast-furnace intermediate carries a leaf class, so no
    metal prices below the material it is refined from.
16. An item a crop drops but another recipe also makes is priced from that
    recipe, not as a crop drop.
17. Every leaf that ships has a weight: a tiered leaf has an `item_tiers` row,
    and a fraction has a priced parent.
18. A recipe accepting any of several items prices from the cheapest of them,
    and changing the weights table can change which one that is.
19. A recipe on a mixed map is illegal at a garage tier below `tier` until the
    map's multiblock is marked built, then legal at `multi_tier`.
20. No recipe from a `*recycling` category ships, and the build reports no leaf
    priced below a millionth of its own weight — the gap between a genuinely
    cheap route and a loop that creates matter.
