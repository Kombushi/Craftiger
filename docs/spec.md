# GTNH Crafting Planner — Specification v1.51

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
  `EU/t ≤ 32 × 4^(n−1)`. TecTech stamps its wirelessly star-powered recipes
  (the Godforge modules) with a sentinel meaning no hatch requirement at all;
  the exporter ships those with a null voltage, which yields no tier, while
  real MAX-tier recipes carry computed voltages and keep theirs. Multiblocks are fed by two 2-amp energy hatches and
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
  and `None` marks a machine as not owned. The default only reaches machines
  whose cheapest block is craftable by then: the artifact ships each map's
  availability era — the era solve's era of its cheapest serving machine
  block (§3) — and a machine with no override whose era exceeds the default
  tier counts as not owned. A recipe's voltage label says nothing about when
  its machine exists; without this gate an HV default would run LV recipes on
  endgame multiblocks (Circuit Assembly Line). A machine whose era the model
  never resolved stays owned at the default — the era solve has reachability
  gaps, and gating on ignorance would turn each one into a pricing hole. An
  explicit per-machine tier always wins — the user saying they built it is
  the authority. For multiblocks the tier means the
  best energy hatch installed. A map served by both kinds of machine carries a
  second switch, whether its multiblock is built, because the hatch allowance
  is worth a tier and belongs to whoever built the multiblock. The EBF is
  configured by two values: voltage tier and installed coil. Five maps carry
  heat requirements: Electric Blast Furnace, DTPF, Digester, and Vacuum Furnace
  take a coil each — per map, since each multiblock is built with its own —
  while the Helioflux Melting Core has no coils and skips the heat check when
  owned (§9). The Electric Blast Furnace alone additionally gains 100 heat per
  garage tier above MV, the energy-hatch bonus; the other coil maps have none
  (both facts verified against GT5-Unofficial source). Crafting table, furnace, and Mining are
  always owned at tier 0.
- **Garage-legal recipe**: `required ≤ effectiveTier(recipe.machine)`, where
  `required` is `recipe.tier`, or `recipe.multi_tier` when the map has one and
  the garage says the multiblock is built. Heat-gated recipes additionally
  require `recipe.heat ≤ maxHeat(their map's installed coil)`, plus the
  Electric Blast Furnace's hatch bonus of 100 per garage tier above MV;
  Helioflux Melting Core recipes skip the heat check entirely (§9). Recipes of
  `None` machines are never legal.
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

Stage 2 — build. The HSQLDB dump is first converted into a local
`artifacts/dump.sqlite` copy by the `dump:convert` task (a JDBC copier in
`tools/dump-convert/` — the only step requiring a JRE, since .NET has no
HSQLDB driver). The builder (`Craftiger.Builder`, a standalone console
project — repo layout in §8) then produces:

- `planner.sqlite` — slim relational data (schema below)
- `atlas.webp` — one texture atlas of all item/fluid icons
- `atlas-offsets.json` — `itemId → (u, v)` pixel offsets into the atlas

These artifacts are the only contract between the builder and the API.
`artifacts/dump.sqlite` is an intermediate — a faithful SQLite copy of the
dump kept locally beside the icon archive `artifacts/image.zip` for builder
runs and ad-hoc queries; neither is committed nor baked into an image.

Builder responsibilities, in order:

1. **Unification** — collapse oredict-equivalent items into one canonical item;
   keep an alias table for search. Which names unify is GT's own call, not a
   config list: the dump's `GREG_TECH_ORE_DICT_UNIFICATION` table carries every
   oredict name GregTech's unificator substitutes, each with GT's target item,
   and `GREG_TECH_UNIFICATION_BLACKLIST` the items GT exempts. A name in the
   table merges its members (minus blacklisted ones) with GT's target as the
   canonical; every other name — wildcards (`ingotAnyIron`, `listAll*`,
   `crafting*`), category names (`dye*`, which would quietly turn limonite
   into cocoa beans; `dustSpace`), and item-kind conventions (`treeLeaves`,
   `fenceWood`, `record`) — registers for leaf classification and search but
   never merges identities: cherry leaves hammer into pink petals, not oak
   leaves into tulips. Oredict names resolve to their exact GT prefix by
   longest match against `GREG_TECH_ORE_PREFIX` — GT's own prefix registry
   with its unifiable / material-based / container flags and material
   amounts — so `dustImpureIron` belongs to `dustImpure`, never to `dust`.
   Material leaf classes (`ingot`, `gem` and its grades, `dust`, `nugget`,
   piles) attach only where the exact prefix is one of theirs and the
   classifying name is one GT unifies, so a convention name that merely
   starts with `dust` cannot hand its members a material leaf, and the
   primary-name pick prefers unified names so `ingotAnyIron` never shadows
   `ingotIron`.
2. **Normalization** — decompose every filled container (cell, bucket) into
   empty container + fluid, then net out items appearing on both sides of one
   recipe. Balanced containers vanish, so cell-only recipes become their
   fluid form automatically; unmatched containers survive as real inputs or
   outputs and stay priced. Then split off non-consumed inputs: the dump marks
   most catalysts (programmed circuits, molds, shapes, lenses) with stack
   size 0, which is the primary signal; crafting tools additionally announce
   themselves through Forge's container-item data — a tool crafts into its own
   worn self, damage in NBT or in the meta, whatever mod it comes from — while
   an item whose container is a different item (a soup bucket leaving its
   bucket) stays a real ingredient, as does a programmed circuit that a
   crafting-grid conversion genuinely consumes. One catalyst condemns its
   whole slot: its members are alternatives for the same role. Condemned
   slots are not dropped — they ship as catalyst-flagged rows (§9): a recipe
   that needs a mortar in place shows the mortar, it just never pays for it.
   A member that is such a wearing tool carries the `tool` flag too, so the
   solver can tell a slot that wears a wrench from one that holds a circuit
   when two routes tie (§5). The Tree Growth Simulator's rows list no
   inputs at all — the sapling sits in the controller slot, the harvesting
   tools in the bus, none consumed — so the builder synthesizes them: the
   map's controller-slot item becomes the sapling catalyst, each output
   class (log, sapling, leaves, fruit — the dump's output slots, in that
   order) gets a catalyst slot holding the best-multiplying tools of the
   dump's probed tool table, and the amounts ship at LV with that tool's multiplier
   applied, on a five-second run drawing LV's practical 30 EU/t; the
   recipe's `overclock` names the tree farm's own ladder (§9).
3. **Exclusion** — drop every recipe source listed under "Excluded by design"
   (§9).
4. **Tier tagging** — per recipe: voltage tier per §2 (GT label). Two tiers
   ship, because 41 of the pack's 167 maps are served by both single-blocks and
   a multiblock, and between them they carry 85% of GT's recipes: `tier` is what
   a single-block needs, and `multi_tier` what the multiblock needs, set only
   where owning one actually lowers the bar. A map with nothing but multiblocks
   has no second option, so its `tier` already carries the allowance and its
   `multi_tier` stays empty. Machine names normalize by stripping the recipe
   map's constant voltage suffix ("Macerator (ULV)" → "Macerator") and then
   applying the builder's rename table: crafting variants merge
   (shaped/shapeless → "Crafting Table") and the EBF map takes its controller's
   name (NEI's "Blast Furnace" → "Electric Blast Furnace"). Any recipe with a
   coil heat requirement keeps it in `recipes.heat` (EBF and its multiblock
   upgrades); the coil list (name and max heat from the dump's coil export,
   tier the coil's own solved era, §3 step 6) is exported into `meta` for
   the garage UI. Byproduct output slots
   open where a map's electric single blocks gain them: the dump carries each
   machine's output-slot count, and slot *i* unlocks at the lowest tier whose
   machine has more than *i* slots (the Macerator's 2nd slot HV, 3rd EV,
   4th IV). Each such recipe splits into a primary-only variant at the map's
   tier plus cumulative variants (`id~b3`, `id~b4`, …) floored at the slot's
   tier, so byproducts stay behind the right garage tier and era, and steam
   macerators grind primaries only.
5. **Leaf tagging** — mark leaves by exact GT prefix and lists (§4).
   Ore-washing and blast-furnace intermediates (`crushed*`, `dustImpure*`,
   `dustPure*`, `ingotHot*`) are never leaves: each is a GT prefix of its
   own, not a kind of dust or ingot, so exact matching leaves them classless
   with no list to maintain — they exist only inside a chain, and a flat
   weight on one would cap every material made through it. What a
   CropsNH crop drops is a leaf too, but only where farming is the one way in —
   anything another recipe also makes is priced from that recipe. Crop drops
   are claimed last so a vanilla farmable oredict always wins, and by item id
   since most carry no oredict. Once eras are known the leaf set is pruned
   again: a tiered leaf the fixpoint never reached, and a fraction whose parent
   is not itself priced, lose their class and fall back to their recipes, so
   every leaf that ships has a weight the solver can work out. The
   minable-block list names blocks by item id, never by oredict: convention
   names like `glowstone` span every planet's variant, and matching them
   would hand Pluto Glowstone an Overworld mining era. This list stays
   builder config because the dump cannot supply it: it names the dimension
   each stone type belongs to, but nothing ties a stone type to the block
   item it places, and no `stone<Dimension>` oredicts exist. A block left
   off the list gets no era at all, so off-world stone stays unreachable
   rather than free.
6. **Ingot tiering** — an ingot's tier is its production era, computed as a
   min-of-max fixpoint over the whole recipe graph:
   `era(item) = min over producing recipes of max(intrinsic recipe tier,
   era of every input)`. World-origin items seed the fixpoint: farmables,
   logs, minable blocks, the world fluids config lists (water, lava), the
   drops of mobs a checked-in list dates (the Wither at Steam, since the
   Nether is — the dump names no mob's world, so an unlisted mob's drops
   seed nothing and its Nether Star would otherwise wait for a ZPM-era ore
   vein), and mined `ore*` items —
   except ores generated only in later worlds, which seed at the era of
   reaching their cheapest generating dimension. That era is derived from the
   dump's GT worldgen tables (veins, small ores, dimension tiers) through two
   builder-config maps: a dimension-tier → era ladder (T1 rocket = HV,
   T2 = EV, …) and per-dimension eras for tier-0 worlds reached without a
   rocket (Nether = Steam, End = HV, Everglades = ZPM, …). Veins disabled in the
   default worldgen config are ignored, and a dimension in neither map
   contributes nothing. A vein's stone-variant blocks pair only with the
   dimensions made of their stone: the Pluto-stone block of a vein that also
   spawns in the Overworld dates from Pluto, not from the Overworld — else
   macerating it would hand out its planet's byproducts (Pluto Ice, and
   through it Black Plutonium) at era 0. A material placed in a single stone
   variant has no planet variant to pair, so its block goes wherever its vein
   spawns: BartWorks ores come in one block whatever the world, and the
   BArTiMaEuSNeK vein on Moon-stone Ross128ba would otherwise seed nothing —
   leaving the material unreachable and its gem, dust and ingot tiered by
   the recycling fallback at their macerator's LV instead of the planet's
   LuV. GT also oredicts every stone variant of an ore
   (`oreSethIceCallistoIce`), including variants no vein places. The worldgen
   tables name their placed blocks by item, and the material's plain `ore*`
   oredict is credited directly beside them, so an ore oredict otherwise seeds
   only on an exact material-name match. A name that merely ends in a spawning
   material matches nothing: `oreCosmicNeutronium` is not a stone variant of
   Neutronium but a material of its own, and a suffix match would date Cosmic
   Neutronium from Neutronium's small ores. Dating unplaced ores from the
   Steam age hands out endgame matter for free: an Ichorium ore block that
   generates in no dimension would otherwise macerate into a Barnarda E stone
   byproduct at era 0. Ore blocks that exist as items but never world-generate
   get no seed at all — their era comes from recipes, or for Space Mining ores
   from the era-only mining maps (§9). Mined small-ore drops and `rawOre*` chunks
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
   heat)`, where a heat's coil tier is the lowest era among the coils
   reaching it. Coil eras are themselves outputs of this solve — coils are
   EBF products — so the solve iterates: every coil starts at era 0, the
   ladder is rebuilt from the solved coil eras, and the solve reruns until
   the ladder stops rising (it rises monotonically, so a bounded number of
   passes settles it; a coil that never becomes craftable leaves the
   ladder). Machine availability gates the era too: a recipe costs what the
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
   A few machines carry a config era floor for the same reason: the Godforge
   upgrade tree gates what the Heliofusion Exoticizer can make, upgrade
   purchases are not recipes, and the quest book anchors magmatter at UMV.
   Steam machines — flagged as such in the dump, never guessed from names —
   run their map's LV-and-below recipes in the steam era, and burning fuel
   instead of EU, they bring no voltage floor of their own. Crop
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
   A recipe whose item inputs are all shapes of the output's own material
   (pile packing, remelting) converts the material rather than producing
   it, so it supplies the fallback tier only when no other recipe exists —
   otherwise the ULV pile packing every dust carries would undercut the
   real route.

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

- `items(id, name_en, oredict, is_fluid, leaf_class NULL, atlas_idx, max_stack
  NULL)` — `max_stack` is the stack size the pack gives the item (NULL for
  fluids), so the UI can read an amount as stacks (§7)
- `item_aliases(item_id, alias)` — merged names and oredict names for search
- `item_search(item_id, text)` — an FTS5 trigram index (`case_sensitive 1`) over
  every item's name and every alias, the text lowercased with invariant
  Unicode case mapping (.NET `ToLowerInvariant`) at build time; the reader
  folds its query the same way, so matching is case-insensitive on every
  script while SQLite's own ASCII-only `LIKE` folding never matters (§7)
- `recipes(id, machine, tier, multi_tier NULL, heat NULL, duration_ticks, eu_t,
  amps, cleanroom, low_gravity, overclock NULL)`
  — `multi_tier` for maps whose multiblock lowers the tier, `heat` for
  coil-gated recipes only. `eu_t` is the per-amp voltage; total power draw is
  `eu_t × amps`, with `amps` from the recipe's own dump row (the exporter
  already folds map amperage into it — the builder warns when a >1 A map's
  recipes diverge from the map, the tripwire for that convention changing).
  `cleanroom` and `low_gravity` carry the recipe's environment requirements
  for rate planning: the factory solve admits a flagged recipe only when
  the garage's era reaches the matching wall in `meta.environment`, and a
  plan with running cleanroom lines hosts them in one Cleanroom line —
  added after the solve, one machine drawing a tenth of the controller's
  40 EU/t recipe overclocked by a hatch of the garage's tier (4 EU/t
  through MV, quadrupling per tier above) — while a low-gravity line
  needs a place, not a machine, and adds nothing. The cost engine keeps
  handling cleanroom through the era solve and ignores both columns.
  `overclock` names the ladder a recipe
  climbs above its tier for rate planning: null for GregTech's standard one
  (each step quadruples power and halves duration), `TREE_FARM` for the
  Tree Growth Simulator's, where each step quadruples power and multiplies
  every output by the tier's yield instead — 2t² − 2t + 5 at tier t, over
  the value at the recipe's own tier — at unchanged duration, `FIXED` for
  per-tier farm rows that never climb, and `EEC` for the Extreme Entity
  Crusher's, quartering duration to a one-second floor and quadrupling
  outputs past it. `scope` names who reads a row: null rows feed every
  engine; `FACTORY` rows — the synthesized Crop Manager, Industrial Farm
  and Extreme Entity Crusher lines — exist for rate planning only,
  `FACTORY_MOB` rows additionally wait for the request's mob-farms toggle,
  and `FACTORY_BRED` rows for its bred-seeds toggle. Crop rows bake fresh
  1/1 seed stats — growth adds to the base speed of 6, gain multiplies
  drop rounds by 1.03 per point and adds (gain + 1)/100 of a bonus drop
  per round — and every row carries a `FACTORY_BRED` twin at the 31/31
  breeding cap. Below the tier-9 fertilizer wall a fertilized twin (`~f`)
  competes against plain sticks; the Crop Manager spends the item
  fertilizers of its choice slot, the Industrial Farm drinks liquid
  fertilizer — both from the dump's fertilizer-registry export, with the
  enriched liquid the fertilizer unit demands told apart as the
  higher-potency one of the exported pair. The farm machines themselves
  come off the dump's classes (crop managers with their tiers, the
  Industrial Farm, the Extreme Entity Crusher), the seed-bed tier span off
  the component export, and the crusher's xp-juice yield off the exported
  constant. Industrial Farm rows additionally
  ship per upgrade build — one slot per structure slice (bed tier − 1):
  the all-accelerator build (`~gau`, +100 % speed and +125 % base power
  per unit), the harvest build (`~hrv`, fertilizer unit at ×1.5 speed and
  +0.5 rounds on enriched liquid, up to two harvesting units at +20 %
  rounds each, accelerators on the rest), and from ZPM the overclocked
  build (`~oc`), the one farm row on the standard ladder
- `recipe_inputs(recipe_id, item_id, amount, slot, catalyst, tool)` — amount
  in units, or mB for fluids; rows sharing a `slot` are alternatives the
  recipe accepts any one of; `catalyst = 1` rows are the tool, mold, and
  circuit slots the recipe needs in place but never consumes — never priced
  (§9); `tool = 1` marks a catalyst row whose item is a wearing tool (§3 step
  2), and whether a slot holds one is the only thing the solver reads of
  catalysts, to break exact ties (§5)
- `recipe_outputs(recipe_id, item_id, amount, chance)` — `chance ∈ (0, 1]`
- `recipe_grid(recipe_id, cell, slot)` — the shape of a shaped crafting recipe:
  each filled cell of the 3×3 grid (row-major 0–8) names the `recipe_inputs`
  `slot` it holds, so the shape is drawn over the folded slots and an oredict
  cell follows the alternative the solve picked (§7). Ingredient and choice
  slots come first, catalyst slots after — the same numbering as `slot`. A
  shaped recipe whose cell lost its ingredient to netting (a bucket that
  comes back out) ships no rows and renders folded; shapeless and machine
  recipes never have rows
- `item_tiers(item_id, tier)` — tiered materials: ingots, gems and dusts (§4)
- `item_parents(item_id, parent_item_id, divisor)` — fraction leaves (small and
  tiny dusts, nuggets, gem grades) name the item their weight divides from
  (§4), with the divisor the ratio of GT's material amounts, resolved by the
  same rule that pruned them, so a shipped fraction always has a priced
  parent and the solver never re-derives the link from oredict names
- `item_weights(item_id, weight)` — weights overriding the item's leaf class,
  where one class covers items worth different amounts (§4)
- `fuels(map, item_id, amount, eu_per_unit NULL, eu_t NULL, duration_ticks
  NULL, return_item_id NULL, return_amount)` — what each fuel-flagged
  recipe map burns, normalized to 100 %
  generator efficiency. Standard rows carry `eu_per_unit`: EU per mB for
  fluids (a cell resolves to its contained fluid, whatever the cell's
  volume) and EU per item for bare solids, which burn as 1000 mB worth —
  both are GT5-Unofficial's own generator math. Lifetime rows carry `eu_t`
  over `duration_ticks` per `amount` consumed instead: RTG pellets (the
  dump's special value is burn years, 365 Minecraft days each) and
  GoodGenerator naquadah fuels, whose special value is the EU/t itself and
  whose recipe returns `return_amount` of the spent fluid `return_item_id`
  per `amount` burned. Each fuel
  map's reading is a checked-in builder classification — Standard, Rtg,
  Timed, Boiler, Excluded (real recipes wearing the fuel flag), or Empty
  (must stay so) — and an unclassified map fails the build. The steam
  carrier adds
  synthesized rows: steam pseudo-fuels at 0.5 EU per mB on the synthesized
  steam-turbine maps, whose machines come off the dump's classes (single
  steam turbines by class, large and XL steam turbines as the STEAM rotor
  kinds, deprecated generations dropped), and one `gtboil~` recipe per
  (large boiler, fuel) boiling water into IC2 steam at the boiler's rate
  over the extracted burn seconds. A boiler's generation is its class
  suffix, matched against the fuel prose's abbreviated names by prefix
  ("Tungstenst." → TungstenSteel); the water per liter of steam is the
  dump's `STEAM_PER_WATER` constant
- `boiler_fuels(item_id, boiler, burn_seconds)` — how long one unit burns
  per large-boiler generation, parsed from the dump's burn-time text;
  "Not allowed" generations ship no row
- `machine_items(map, item_id, tier NULL, multiblock, steam, era NULL)` —
  every machine block serving a recipe map, the per-block flip side of
  `machine_eras`; rate planning picks the serving block per recipe from here.
  `era` is the block item's own craftability era where the era solve reached
  it. Blocks whose tooltip carries GT's rigid deprecation banner are
  superseded controllers and never ship
- `machine_props(item_id, era NULL, generator_efficiency NULL, generator_eu_t
  NULL, generator_amps NULL, dynamo_eu_t NULL, dynamo_amps NULL, max_parallel
  NULL, boiler_eu_t NULL, rotor_fuel NULL)` — rate-planning stats of one
  machine block, merged
  from the dump's generator, dynamo hatch, large boiler, and multiblock
  exports, plus what the machines' exported classes say: an XL turbine's
  `max_parallel` is the exported slot-count constant (the prototype's own
  parallel reading needs a live structure and is overridden), and
  `rotor_fuel` names the rotor stat class — `GAS`, `PLASMA` or `STEAM` —
  a rotor-driven controller spins by its class name, null on every other
  block (the HP and SC steam kinds burn fuels the model does not rate and
  stay unclassified); `era` is
  the block's own craftability era where the era solve reached it. Only
  blocks carrying signal ship a row — a bonus-less multiblock at one
  parallel is the model's default
- `machine_bonuses(item_id, kind, bonus, multiplicative, tier_axis NULL)` —
  a multiblock's typed parallel/speed/EU bonus lines, straight from the
  dump's tooltip-template export; a steam multiblock's `STEAM_DISCOUNT`
  ships as `EU_DISCOUNT`, since steam is those machines' power; `bonus` is the
  displayed number (220 for "220 % Speed"), `tier_axis` the scaling axis
  (`VOLTAGE`, `COIL`, …) of per-tier kinds
- `generator_modes(item_id, kind, fluid_id, per_second, factor)` — the
  consumable modes of the boosted generator multiblocks, from the dump's
  engine and reactor exports (a reactor's COOLANT factor arrives as a
  percentage and ships as the multiplier). A combustion engine (its nominal EU/t rides
  `machine_props.generator_eu_t`) carries a `BOOSTER` row (the gas that
  multiplies output by `factor` at `per_second` liters) and a `LUBRICANT`
  row (drain unboosted; boosting doubles it); a reactor carries an `UPKEEP`
  row (flat drain), `COOLANT` rows (each multiplies output alone by
  `factor`) and `EXCITED` rows (each multiplies output and fuel together).
  The BOOSTER rate is the builder's 2 L/t and the LUBRICANT rate its
  1 L per 72 ticks, each times the engine's exported additive factor; the
  boost factor is the ratio of the exported efficiencies. Engine burn
  mechanics are GregTech code constants and live in the solver:
  fuel per tick is the nominal output over the fuel value in integer
  division, boosted burns double that plus a weighted expected top-up on
  fuels richer than the nominal output, which refuse to run unboosted;
  engines emit through one classic dynamo hatch with voiding, while a
  reactor stops rather than voids, so a mode combination no buildable hatch
  covers ships no line
- `turbine_rotors(item_id, size, material, durability, base_efficiency,
  overflow_tier)` and `rotor_fuel_stats(item_id, fuel, efficiency,
  loose_efficiency, optimal_flow, loose_optimal_flow, optimal_eut,
  loose_optimal_eut)` — every rotor variant's computed large-turbine stats,
  tight and loose fit, per `STEAM`/`GAS`/`PLASMA`; flow is L/t for steam and
  EU/t of fuel value otherwise
- `renewable_seeds(item_id, kind)` — the auto-infinite primitives: items
  obtainable automatically and forever, from which run-time derivation
  through garage-legal chains starts. Only `WORLD` rows ship, from a
  curated name list (water, air, cobblestone — never lava): crops,
  farmables and mob drops are derived through factory-scoped farm lines
  or bought at their weights, never free of machines
- `machine_eras(machine, era, multiblock)` — per map, the era solve's era of
  its cheapest serving machine block, floored by any configured gate; null
  where no block ever becomes craftable, 0 for maps served without machine
  blocks (crafting grid, synthesized world recipes). `multiblock` marks maps
  every recipe of which lacks a single block — they only ever run as
  multiblocks, and the garage lists them apart (§7). Drives the
  garage's default-ownership
  gate (§2).
- `meta(key, value)` — `schema_version` first of all: the version of this
  contract, bumped on any schema change, so a reader can refuse an artifact
  written for a contract it does not know. Beside it `build_id`, a fresh
  identifier per build — the same pack at the same schema is rebuilt, and a
  reader that keeps solved tables outside the process keys them by the exact
  build (§8) — the pack version, dump date, atlas dimensions, coil list,
  `tier_voltages` (EU/t per amp per tier, indexed like `tier_names`, 0 for
  Steam — the client never hardcodes the voltage ladder), `steam` — the
  carrier's pack facts as JSON: `SteamFluidIds` (every fluid that counts as
  steam, in the builder's configured order, limited to fluids the dump
  knows), `DistilledWaterId` (what turbines condense steam into; null when
  the dump lacks the fluid), `EuPerLiter` (0.5) and `WaterPerSteam` (the
  dump's `STEAM_PER_WATER` constant, 160)
  — so the runtime carries no modpack ids of its own — `environment` —
  the environment walls as JSON: `CleanroomItemId` and `CleanroomEra`,
  the Cleanroom Controller's canonical item and its solved era (falling
  back to the configured HV floor when the dump lacks the controller),
  and `LowGravityEra`, the configured era of the first rocket — and the
  price check's verdict (§3 step 7).

The artifact is written once and shipped read-only. Journal sidecars left by
an interrupted run are cleared before writing, alternatives, aliases, and
atlas cells are unique by constraint rather than by convention, and the file
ships `ANALYZE`d so a reader's query planner sees real row counts.

## 4. Cost model

**Material cost only** (exclusions: §9).

### Leaves and weights

| Leaf class | Membership rule | Default weight |
|---|---|---|
| Minable block | explicit list, each with the era of the cheapest world it is mined in (End Stone at HV) | 1 |
| Ingot | GT prefix `ingot` | `B × 4^tier` (see below) |
| Dust | GT prefix `dust` | `B × 4^tier`, from the matching `ingot*` or `gem*` where the material has one, else from the dust's own era |
| Small / tiny dust | GT prefix `dustSmall` / `dustTiny` | parent dust ÷ 4 / ÷ 9 |
| Nugget | GT prefix `nugget` | parent ingot or gem ÷ 9 |
| Gem | GT prefix `gem` | `B × 4^tier`, tiered like an ingot |
| Gem grade | GT prefix `gemChipped` / `gemFlawed` / `gemFlawless` / `gemExquisite` | parent gem × ¼ / ½ / 2 / 4 |
| Log | oredict `logWood` | 1 |
| Farmable | explicit list: sugar cane, seeds, saplings, crops, … | 1 |
| Crop drop | what a CropsNH crop drops, where no other class claims it | 1 |
| World fluid | explicit list: water, lava, oil and its cuts, natural gas | per fluid, from `item_weights`: water 1, lava 2, oil and gas 8 |

Membership goes by the oredict's exact GT prefix (§3 step 1), and every
fraction divisor is the ratio of GT's material amounts for the two prefixes —
a nugget is 403200 of an ingot's 3628800. Ore-washing and blast-furnace
intermediates (`crushed*`, `dustImpure*`, `dustPure*`, `ingotHot*`) are
**not** leaves in any class — see §9.

All rules and weights live in **one editable weights table** (config UI, §7).
An `item_weights` row overrides its item's class, for a class whose members are
not worth the same. The defaults are deliberately crude; the table is the
tuning surface. Runtime edits are per item in v1: a user's item weight beats
the artifact's `item_weights` row, which beats the class rule — the class
rules themselves are fixed, and leaf membership is baked at build time. A
fraction has no rule of its own to override: it follows its parent's resolved
weight — overrides included, the two being the same material — through the
shipped `item_parents` link, unless the fraction itself is overridden.

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
  decided at build time. The alternative a recipe was priced with is recorded
  when it wins an item (§5); that stack is what the BOM walk expands and the
  item detail shows.
- **Chanced outputs use expected value** — dividing by `chance` prices the average
  number of runs needed. It also keeps otherwise-chance-only items reachable.
- **Each output is priced independently** from the full input cost of its
  recipe (byproduct exclusion: §9).
- **Fluid costs are per mB** — a fluid's `cost` is per millibucket and recipe
  amounts are in mB, so the formula needs no special casing. Steam consumed
  as power is energy, not an ingredient (§9).

## 5. Cost engine

**Costs are solved by a strict-improvement worklist fixpoint; the resulting
`bestRecipe` pointers form a DAG, except where a set of recipes feeds itself
while consuming something from outside — those are loops, and §6 plans them.**

Mechanism:

1. Initialize `cost[leaf] = weight`, all other items `+∞`.
2. Queue every garage-legal recipe. Pop a recipe; if any input is `∞`, skip.
3. For each output, compute the candidate (§4). Update `cost` and `bestRecipe`
   only on strict improvement: `candidate < cost[item] − ε`, `ε = 1e-9`.
4. On update, re-queue every recipe consuming the improved item. Stop at empty queue.

Why this shape:

- A cycle can never strictly undercut itself (ingot → block → ingot re-offers the
  same price), so shape-shuffles starve instead of oscillating, and `bestRecipe`
  stays acyclic through them. A cycle that consumes an outside input while
  multiplying its own item — hammer a Raw Crystal Chip into nine Parts,
  autoclave a Part with Europium back into a Chip — undercuts itself by a
  shrinking margin each pass and converges geometrically to the right price
  (a chip costs 9/8 of the europium one pass burns); the last pass that still
  clears ε leaves its recipes pointing at each other. That pointer loop is not
  an error: it is the plan's honest shape, and §6 sums it.
- **A slot's choice is made when the recipe wins, not when the BOM walks.** The
  solve records the alternative each slot was priced with; every later reader —
  the BOM walk, the reroute guard below, the item detail — replays that record.
  Recomputing "cheapest" at walk time would reopen the choice on exact ties,
  and ties arrive after the win: the circuit wrapper's oredict slot lists the
  wrapper itself, a GT dye's crafting slot lists that very dye, and once the
  output is priced it ties the alternative it was priced from. Taking it would
  point the item at itself and break the DAG the fixpoint proved. Only the
  recorded edges carry the acyclicity argument. Recipes that never won an item
  (a pin, a candidate in the detail view) still resolve to the cheapest
  alternative, first on ties.
- Costs only decrease and are bounded below by 0, so termination is guaranteed.
- **Better routes win exact ties.** Recycling shape-shuffles equalize every
  form of a material at one price, with dust as the entry form (maceration
  outputs dust; the ingot smelts from it 1:1), so material forms tie
  constantly and the winner would fall to fixpoint race order. A cost
  surcharge cannot separate a 1:1-derived pair — the ingot inherits its
  dust's price, penalty included — so the preference runs after the solve
  instead. Each garage-legal producer at the same price within ε scores a
  composite key over its chosen inputs and its catalyst slots, compared
  lexicographically:
  1. *Form rank* — leaf classes carry a configured priority (ingot and gem
     first, then dust, then nugget, then the piles; unlisted classes and
     non-leaf inputs rank best), and a recipe scores as the worst form it
     consumes.
  2. *Chain depth* — how many chosen-edge steps sit below the recipe's
     deepest input, so consuming a leaf directly beats a mass-conserving
     detour through intermediates (melting the ingot beats rodding it and
     melting the rod).
  3. *Leaf weight* — the heaviest chosen leaf's resolved weight (§4), so
     among cost-tied leaves the lower-era material wins (plain steel beats
     magnetic steel, whose polarizer chain equalized the price but not the
     era ceiling).
  4. *Tool slots* — how many of the recipe's catalyst slots hold a wearing
     tool (§3 step 2), fewer first: catalysts never price, so a hand craft
     that wears a wrench ties the assembler that only needs a circuit in
     place, and the machine is the route worth planning — four rods assemble
     into a frame box rather than wrench into one, an ingot macerates into
     dust rather than grinds under a mortar. A tool route that is genuinely
     cheaper, or shallower, still wins: the key is the last one judged.
  `bestRecipe` moves to the best-scoring producer — unless that recipe's
  chosen inputs can reach the item over chosen edges, where rerouting would
  close a pointer loop, so the incumbent is kept. The reachability walk runs
  through produced leaves: the BOM stops at leaves, but the pointer graph
  itself must stay a DAG, or two forms at an exact plateau end up pointing
  at each other (macerate-the-ingot ↔ blast-the-dust) and neither can be
  traced to raw materials. Depths are measured on the DAG as the solve left
  it. Costs never change in this pass,
  ties still lose during the solve, and the walk then ends on the best
  form's leaf. A route where a worse form is genuinely cheaper is a real
  gap, not a tie, and keeps winning.
- Full-pack scale (a few hundred thousand recipes) converges in about a second
  in memory. The recipe graph is held only as integer positions and
  compressed-row arrays (`SolverIndex`), built once when the artifact loads by
  streaming its rows — recipe records exist only as an input form for
  fixtures — and never as per-recipe objects or id-keyed lookups; the solved
  table is stored the same way — cost, best recipe and the
  picked alternative per slot as arrays over index positions, a few megabytes
  per solve; readers ask it by position on hot paths (the BOM walk) and by id
  at the API edge. The layout changes nothing about the result — evaluation
  order, tie outcomes and the recorded alternatives are exactly those of the
  id-keyed walk it replaced.

Caching and pins:

- Solved cost tables are cached keyed by `(garageHash, B, weightsHash)`, where
  `garageHash` covers the default tier, all overrides, and the EBF coil.
- A solve never blocks anyone else: concurrent requests for the same key share
  one computation and wait only for it, requests for other keys — cached or
  new — proceed at once, and there is no cap on how many distinct solves run
  at a time. A solve that fails is not kept; the next request recomputes.
- **Pins never enter the cost-solve cache key.** The sorted list always shows the
  unpinned baseline; pins are applied as an overlay when resolving the item detail
  view and the BOM walk. v1 simplification: a pin changes recipe *choice*, not the
  listed price. The factory solve is the exception: there pins shape the feasible
  set itself, so its own cache key hashes them (§8).
- A pin whose recipe the garage cannot run is ignored with a visible red
  warning, falling back to auto-cheapest. Pins cannot bypass the garage filter.
  A pin that would close a cycle in the BOM walk is likewise ignored with a
  warning (§9).

## 6. BOM computation

**Totals are computed on the chosen-edge graph of `bestRecipe` pointers (pins
overlaid); the rendered grid is a projection of that result.**

1. Seed `demand[target] += count` for every cart entry — multiple targets merge
   automatically.
2. Walk the graph's strongly connected components in reverse topological
   order — every consumer before its producers. For a lone non-leaf item:
   `runs = demand / (output_amount × chance)` of its chosen recipe — summed
   over the recipe's output rows for that item where chanced twins repeat it —
   then add `runs × amount` to each input's demand, taking the alternative the
   solve priced each slot with (§5). Runs and amounts are fractional expected
   values throughout; display rounding belongs to the UI.
3. A component of several items, or an item whose recipe consumes its own
   output, is a **loop**. Its members' demands are one linear system —
   `demand = outside demand + gain · demand`, where `gain[i, j]` is how many
   units of member *i* one unit of member *j* burns through *j*'s recipe — solved
   exactly by elimination. The system has a finite, non-negative answer exactly
   when the loop's gain is below one (`I − gain` is an M-matrix), which is the
   same condition under which the cost fixpoint converged on it (§5): the
   crystal chip loop needs 9/8 chips of autoclave runs per chip delivered, and
   18 mB of europium. A loop is **seeded once**: one unit of the member with the
   cheapest garage-legal producer outside the loop — whose chosen inputs do not
   reach back into it — is planned through that producer, as the first chip is
   made from the 10 % gem route in the game. The seed's unit counts: the loop
   only produces what the outside demand and its own feeding need beyond it,
   floored at zero member by member (a member the seed covers entirely drops
   out of the system and is solved around), so one chip is the seed route
   alone, two chips are the seed, one hammer run and two autoclave runs, and
   nine parts are one hammer run on the seed chip. A loop nothing outside can
   produce keeps its steady-state totals and warns `loop_unseeded`. A loop whose
   gain is not below one is no plan at all: if a pin built it the pin is
   ignored with a warning (`pin_cycle`); without a pin the solve itself is
   inconsistent and the request fails.
4. Leaves accumulate into the final `item → amount` map and never expand, even
   where a recipe undercut their weight; fluid amounts stay in mB.
5. The same walk carries a second, whole-run accounting: demand seeded with
   the integer target counts, each item's accumulated whole demand rounded up
   once to `wholeRuns = ⌈demand / expected yield⌉`, and `wholeRuns × amount`
   propagated to the inputs. A machine takes a full recipe or nothing, so a
   plan a player can execute never contains a partial craft; because rounding
   happens after the full demand accumulates, shared intermediates round once
   for the whole request, not once per consumer. Inside a loop the same
   equations iterate on integers — round each member's runs up, re-propagate,
   repeat — until nothing changes; the iteration only grows and is bounded by
   the fractional answer, so it settles (eight chips: eight autoclave runs and
   one hammer run beside the seed). Chanced outputs still divide by chance (§4), so whole runs
   cover their demand in expectation only. All whole-run demands are integers
   by construction.

Output per request: per-target direct inputs (chosen recipe, `runs × amount`
per input), merged leaf totals in both accountings, warnings (ignored pins,
unreachable targets, unseeded loops), and the chain nodes — one entry per
expanded item, consumers before producers with targets first, carrying total
demand and runs in both accountings, the chosen recipe with its display data,
the chosen input stack per slot for a single run, and the recipe's full output
rows. Loop members carry their loop's number; a loop's seed is one more node
for the same item, flagged as the seed, with the outside recipe and its single
unit. The nodes are the walk itself made visible: a chain renderer draws them
without re-deriving any choice the walk already made.

## 7. UI

Single-page app, English item names (dump locale), four routes in one
topbar — **Crafting** (`#/`), **Factory** (`#/factory`), **Planner**
(`#/planner`), **Price list** (`#/list`) — with a per-route status readout
and the global Weights button (weights feed every solver). Transient errors — an
unreachable API, a failed solve — surface as self-dismissing toast
notifications, never as layout-shifting inline rows; BOM warnings stay inline
with the results they describe. The planner's sidebar (cart + garage) resizes
by dragging its edge, double-click resetting the default, and a top-bar menu
button hides it entirely; both preferences persist in `localStorage` like all
user state. Item slots carry the planner's own tooltip, not the browser's: a
chrome panel that follows the cursor NEI-style and flips to stay inside the
window, with the item's name on its first line — the display names
unification merged away appended, "Tin Nugget (aka Tin Oreberry)", so a
canonicalized ingredient stays recognizable; oredict-style aliases never
show — and the slot's figures under it in mono. A whole amount of a
stackable solid item (`items.max_stack` from 2 to 64) also reads as stacks,
`5×64 + 15`, on the chain's per-slot totals, the leaf cards and the raw and
derived material grids; never for fluids, catalyst slots, unstackable
items, amounts under one stack, or the per-craft amounts of item detail.
Screens:

- **Search** — type-ahead over canonical names and oredict aliases: a
  case-insensitive substring match (every script, case only — diacritics are
  not folded) answered by the artifact's trigram index with every match for
  queries of three characters or more, and by a scan of the same folded text
  for shorter ones; results are the cheapest matches first, then by name, at
  most fifty, showing icon, name, and cost. Search works before the first
  solve — costs are simply blank until one exists, so results come in name
  order. An item nothing in the pack produces and that is not
  a raw material — a deprecated controller kept only for its conversion
  recipe, a creative or placeholder item — reads `uncraftable` instead of a
  cost, before and after a solve; items merely unpriced under the garage keep
  their `∞`.
- **Craft list** — cost-ascending; grayed `∞` section at bottom, `uncraftable`
  items reading as such inside it; "hide unreachable" toggle; tapping opens
  item detail.
- **Item detail** — all garage-legal producing recipes with their candidate
  costs, current pick highlighted, pin/unpin button per recipe, and an
  add-to-cart button.
- **Cart** — targets with count inputs. Nothing recomputes while settings
  change: an explicit **Calculate** button applies cart, garage, `B`, and
  weights in one go, and a banner marks the shown results stale once those
  drift from what was applied. Pins are the exception — they overlay recipe
  choice only (§5), so a pin re-walks the BOMs on the live solve immediately.
- **Result** — built from one square component (CSS offsets into `atlas.webp`,
  count badge per square, `1.2k`-style formatting; fluids render as their cell
  icon with mB amounts):
  1. *Raw materials* — the merged leaf totals of the selected chain card,
     with that selection's total cost shown behind a `₴` sign; fluids close
     the list, and the warnings row always stays cart-wide.
  2. *Derived materials* — the selection's intermediates grouped by distance
     from the leaves: level 1 is crafted straight from raw materials, each
     further level from the ones before it, cost-sorted within a level with
     fluids last. A range slider reveals levels cumulatively up to the chosen
     one (debounced ~300 ms) and resets to zero on each calculation; the
     selection's own targets never appear, while another cart target sitting
     inside the viewed chain shows at its level like any intermediate.
  3. *Crafting chain* — one flow graph per cart target on a pan/zoom canvas,
     rendered straight from the BOM chain nodes (§6): every expanded item is a
     recipe card (machine, tier, heat, runs, chosen input stacks, output rows,
     duration, EU/t) laid out in topological layers, edges running from each
     producer to the slots that consume it; leaves close the left edge as
     material cards. A toggle beside the fit button turns the graph vertical:
     leaves line the top edge, layers stack down to the target at the bottom,
     and edges leave each producer below its output squares to enter the
     column of the square that consumes them. Cards keep their inputs-left,
     outputs-right reading either way, horizontal is the default, and the
     choice persists (§8). A loop's members sit in one layer tagged `LOOP`,
     the edge that runs against the flow between them is dashed, and the seed
     is its own card tagged `SEED`, feeding the loop member that consumes its
     item. With two or more targets an extra `Σ` card renders
     every target's combined plan in one graph, and the sections above follow
     the selected card. Cards link into item detail for pinning. Catalyst slots
     (§9) render dimmed among the inputs, marked as needed in place but not
     consumed, in both recipe cards and item detail. A shaped crafting recipe
     draws its inputs on the 3×3 grid as the pack crafts it (§3
     `recipe_grid`): each cell shows the chosen alternative of the slot it
     holds, the tool cell dimmed, empty cells blank; the cells carry no count
     — the header's run count is the count, and a cell is one item per craft,
     which its tooltip spells out — and any slot the grid does not place (a
     fluid split from a bucket) sits under the grid with its count. Shapeless
     crafting and machine recipes keep the folded slots. Item detail draws
     the same grid, with the alternatives badge on oredict cells.
  Displayed runs and amounts are the whole-run plan (§6) — a machine takes a
  full recipe or nothing, so no card ever shows a partial craft; the
  fractional expected values surface only in tooltips. A recipe card's own
  output square carries two counts: what the whole runs produce and, in the
  accent color, what the plan actually needs — the surplus is their
  difference, read off the card rather than a tooltip. A chanced recipe may
  list the same item on several output squares; the needed count appears on
  the first of them only.
  Tapping any square opens that item's detail.
- **Garage** — global default tier (Steam…max) plus a machine list
  **filtered to relevance**: only machines in the current cart's upstream
  closure (§2) get a picker; a "show all machines" toggle reveals every machine
  that has recipes. Hidden machines inherit the global default. With an empty
  cart, the list starts empty apart from the toggle. One picker per shown
  machine (`inherit / None / Steam / LV / …`); each coil-gated map's row (§2)
  has a coil dropdown under its tier picker while the machine is built —
  once it reads None or Not built, whether picked outright or by lowering the
  default tier below the machine's era, the dropdown hides and the coil is
  forgotten; crafting table, furnace, and Mining are shown as always-owned. Multiblock-only maps
  (`machine_eras.multiblock`) list apart under a "Multiblocks" header, their
  picker reading `Not built / <tier> hatches` — the same tiers, named for the
  energy hatches feeding the controller. A machine first craftable beyond the
  default tier (§2) is dropped from the relevance list entirely — the default
  garage cannot own it, so it only appears under "show all" or once the user
  has configured it explicitly; there its inherit state reads "Not built",
  and picking an explicit tier claims it. The factory tab shows the same
  garage, its relevance filter walking the deep closure — through
  leaf-class items — because the factory solve expands them (§8 `deep`).
- **Factory** — the rate planner over the same garage, weights and pins.
  Targets are rates: a produce or consume row is an amount per window
  (ticks, seconds or minutes) with the normalized per-second rate read
  back beside it; produce/consume is a segmented toggle and consume rows
  keep a persistent visual cue. One energy row asks for net exported power
  as amps × tier — the live EU/t from the meta tier voltages, also editable
  directly — and its tier floors the generators' output tier. Beside the
  targets sit the mob-farms and bred-seeds toggles and the layer-priority
  picker; the active pins list in the sidebar with a clear button each,
  because the factory cache keys on them (§8) and an invisible pin would
  shape plans silently. An explicit **Solve** posts the whole context; a
  banner marks the shown plan stale once targets, garage, weights, pins,
  toggles or priority drift from what solved it. Results mirror the
  crafting reading order: structured warnings first (a row naming an item
  opens its detail), a totals strip — priced inflow rate, machine draw,
  net export while generators run, and the whole-machine count — then the
  entering streams (auto-infinite ones marked **∞**), the byproduct and
  surplus streams as `+rate` badges, then one flow graph of the whole
  plan, not per target: a card per machine line with the machine count as
  its badge (runs, busy machines, parallels and OC steps in its tooltip),
  `OC×n`/`P×n` chips, the after-OC duration and per-instance EU/t in the
  footer — a generator's footer shows its emission — and the per-item
  stream rates on the slots; streams entering from outside get source
  cards. Layers come from SCC condensation ordered by longest path, so a
  loop's members share one `LOOP`-tagged layer; there are no seed cards —
  a steady state has no first unit. A line run on assumptions — a
  durationless converter, a machine without curated bonus data — carries
  an `EST` chip whose tooltip states the exact assumption, and the plan
  gets one accent-styled note; unaffected cards carry nothing. A plan
  whose status is not solved keeps the results empty beside its warnings.
  The global rate-unit picker (per tick, second or minute; default
  second) converts every displayed rate, is display-only, and never
  travels in the request or the cache key. The target list, toggles,
  priority and unit choice persist (§8).
- **Planner** — the manual pipeline grid over the same engine (§8 `steps`
  and `supplies`): the canvas is the document. Right-clicking the canvas
  opens the create menu — there is no standing search box — and places a
  node at the clicked spot: **steps** (a recipe from the all-scope
  producer catalog, farm rows included, or a generator line), **Inputs**
  (free sources; unbounded by default, a rate turns one into a consume
  target the pipeline must absorb), **Outputs** (produce targets, rate
  edited on the node) and one **Energy** node (amps × tier, fed by
  generator steps); the item behind an Input, Output or step is named in
  a search dialog the menu opens. The solver draws every edge; flows are
  never hand-wired. The producer picker renders each candidate as a mini
  recipe card: the machine block's icon and tier, duration and EU/t, the
  input stacks, and every output with its amount and chance. Nodes drag with
  snap-to-grid and keep their positions; **Tidy** re-runs the layered
  auto-layout. The solve is live, debounced a breath after every edit
  that changes the derived request (drags never re-solve); it runs once
  at least one target-bearing node and one step or unbounded Input exist,
  and only the first solve after a garage or weights change pays for the
  cost solve behind it. A step card shows the solver's chosen block,
  overclock and whole-machine count with **LOCK** and ± overclock nudges
  in place; a generator step has nothing to lock. Whatever the solve
  supplies from outside without an Input node renders as a dashed
  **ghost** card — clicking it offers *make it an Input* or *add a
  producing step* — and real surplus without an Output node ghosts
  symmetrically, offering *make it an Output*; that is the building loop,
  with the plan solved at every stage. "Start from the Factory plan"
  copies the automatic tab's solved lines onto the grid. There is no
  priority picker (the planner always sends machines → resources →
  energy), no pins (§9), and no scope checkboxes: placing an EEC or
  bred-variant farm node is the consent the mob-farms and bred-seeds
  toggles carry on the Factory tab. The nodes persist with their
  positions under `gtnhp.planner` (§8); the plan stays in memory.
- **Config** — the `B` input and the editable per-item leaf-weights table (§4)
  live in a separate weights window; both apply on the next Calculate. Leaf
  membership — which blocks are minable, which fluids are world fluids — is
  baked into the artifact and only changes with a rebuild.

## 8. Architecture

### Repository layout

- `src/Craftiger.Builder/` — standalone .NET console project; NESQL dump in,
  artifacts out. Runs offline on demand, is never deployed, and holds no
  project reference to or from the API — the artifacts (§3) are the contract.
  Paths, pack version and every builder-config list live in `appsettings.json`,
  bound through `IOptions`; the tests run against that same file.
- `src/Craftiger.Solver/` — pure class library: the cost engine (§5), BOM
  computation (§6) and the factory solve. No I/O and no dump dependency;
  referenced by the API and exercised directly by fixture tests. Its
  `Models/`, `Interfaces/` and `Services/` are grouped by area — `Graph`
  (the positional index), `Costs`, `Bom`, `Factory`, `Lp` (the
  `ILinearProgramSolver` abstraction: columns, rows, prioritized
  objectives) and `Options` — with namespaces following the folders.
  Models are records that carry their own rules (a machine block knows
  whether a garage can build it, a dynamo what it nets from a raw output,
  a cost table how a recipe prices against it); GregTech mechanics that
  hold for every pack — the voltage ladder, overclock steps, the Enet
  loss, steam machine duty — live here as value objects, while every
  modpack id and rate comes from the artifact. Recipe legality stays a
  service because it reads the configured garage rules. Tuning constants
  (the cost epsilon, the pruning bands, the layer corridor) are options
  records bound through `IOptions`, the only package the library
  references, so it stays managed-only.
- `src/Craftiger.Solver.Highs/` — the one impure solver piece: the adapter
  implementing `ILinearProgramSolver` over the bundled native HiGHS library
  (`Highs.Native`, ≥ 1.9 for native lexicographic objectives; 1.15.1
  shipped), split into a model loader, the lexicographic layer runner and
  an equilibration value object, each behind an interface, with its
  numerics in an options record. One solver instance per solve — never
  shared, the native library's thread safety is undocumented — pinned
  single-threaded with a fixed seed so identical programs return identical
  solutions on every replica. Registered in DI by the API; the Solver
  project never references it.
- `src/Craftiger.Api/` — .NET minimal API.
- `tests/` — one xUnit project per production project:
  `Craftiger.Builder.UnitTests` runs the builder over a hand-written fixture
  dump, `Craftiger.Solver.UnitTests` exercises the pure engines on fixture
  graphs with a recording LP fake, `Craftiger.Solver.Highs.UnitTests` solves
  end to end through the native adapter, and `Craftiger.Api.UnitTests` boots
  the API over a hand-written artifact.
- `web/` — React SPA.

### Runtime

- **Backend**: the minimal API, stateless, deployed on the `ryokutek` k8s
  cluster as a container image (`src/Craftiger.Api/Dockerfile`, built from the
  repo root) with the three artifacts
  baked in. Loads `planner.sqlite` read-only into memory at startup — refusing
  an artifact whose `schema_version` it does not know before serving anything —
  and holds the solver and its cost-table cache in memory. The artifacts
  directory and the garage rules of §2/§9 (always-owned machines, heat-exempt
  and heat-bonus maps) are configuration.
- **Solve store**: solved entries also live in a Valkey server, reached
  through StackExchange.Redis (RESP, zero-copy binary values), so any replica
  serves a solve another one computed and a restart keeps them. Two tiers: an in-process LRU of a few entries, and
  behind it the store, keyed `craftiger:{schema}:{pack}:{build_id}:{solveId}`
  — the artifact's exact build is in the key, and inside the value, so a
  rebuilt artifact never reads another build's tables (§9). The value is the
  whole entry as compact little-endian binary: the garage and weights that
  produced it (a replica that only ever sees the solveId still needs them for
  pins, seeds and legality), the table's arrays, the craft-list ranks,
  reachable count and converged flag — Brotli-compressed at the fastest
  level behind a clear magic and format version, about 0.7 MB for a full
  solve; a value in any other format is recomputed, never served. A solve checks the
  store before computing and writes the result to the store before answering
  — a few milliseconds on a solve of a second, so the request that follows
  may land on any replica and find it; a failed write is logged and costs a
  later process one recompute, never the response; every read endpoint reads
  through on an in-process miss, so a 404 means no replica ever solved it. Values carry no expiry: the server must evict by
  LRU (`maxmemory` with `allkeys-lru`; the repo's `docker-compose.yaml` runs a
  local one that way, `task valkey`). The connection string
  (`ApiOptions:Valkey:ConnectionString`) is mandatory — the API refuses to
  start without it or without reaching the server — and a store error at
  request time surfaces as an error, never as a silent fallback.
- **Frontend**: React SPA served statically by nginx in its own container
  (`web/Dockerfile`). Both containers share one public origin: the cluster's
  reverse proxy routes `/api` and the two atlas paths to the API and
  everything else to nginx, so the SPA's relative fetches need no
  configuration.
- **Client state**: everything user-specific lives in browser `localStorage` and
  travels with each request — the API stores nothing per user.

### Endpoints

- `POST /api/solve` — body `{garage, b, weights}` →
  `{solveId, pricedItems, converged}`; garage is `{defaultTier, machines:
  {name: tier | null}, builtMultiblocks: [name], coils: {map: coilName}}` and
  weights is the per-item override map (§4). Runs or reuses a cached cost
  solve; the id is a content hash, so identical settings share an entry. A
  `404` on any later call means the cache entry was evicted — the client
  re-posts.
- `GET /api/search?q=&solveId=` → `[{itemId, name, atlasIdx, cost}]`; `solveId`
  is optional so the cart can be built before the first solve — without it
  every cost is null.
- `GET /api/list?solveId=&page=&hideUnreachable=` → cost-sorted page
- `GET /api/item/{id}?solveId=` → producing recipes with candidate costs, the
  solver's current pick (`bestRecipeId`) so the detail view can highlight what
  the BOM will expand, each recipe's `grid` (nine cells → the slot each holds,
  slots first then catalysts, or null; null for a recipe without a shape),
  and an `items` display lookup (name, atlas index, fluid flag, leaf class,
  cost, stack size) for every item id the recipes reference.
- `GET /api/machines?targets=&deep=` — upstream-closure machine list for the
  given item ids; drives the relevance-filtered garage. The default walk
  stops at leaf-class items the way a BOM does; `deep=true` walks through
  them, matching the factory solve's expansion.
- `POST /api/bom` — body `{solveId, targets: [{itemId, count}],
  pins: {itemId: recipeId}}` → `{targets: [{itemId, count, recipeId,
  inputs: [{itemId, amount}]}], leaves: [{itemId, amount, wholeAmount}],
  warnings, nodes: [{itemId, amount, runs, wholeAmount, wholeRuns, recipeId,
  machine, tier, multiTier, heat, durationTicks, euT, inputsPerRun: [{itemId,
  amount}], outputs: [{itemId, amount, chance}], loop, seed, grid}], items: {itemId:
  {name, atlasIdx, isFluid, leafClass, cost, uncraftable, maxStack}}}` — the chain nodes of §6 in both
  accountings plus the same display lookup, so one request feeds a whole
  chain view.
- `POST /api/factory/solve` — body `{garage, b, weights, targets: [{kind,
  itemId, rate, generatorTier}], priority, pins, mobFarms, bredSeeds}` where
  `kind` is `produce`, `consume` or `energy`, `rate` is units per second (EU/t
  of net export for energy) and `priority` orders the lexicographic layers
  (`resource`, `energy`, `machines`; empty means that order) →
  `{factoryId, status, lines, flows, inflows, warnings, pricedInflowCost,
  drawEuT, exportEuT, busyMachines, items}`. Each line carries its recipe,
  machine map, machine item id, runs/s, OC steps, parallels, busy-machine
  count, after-OC duration and per-instance EU/t (negative for a generator's
  net emission; line EU/t is the product with the busy count), plus the
  durationless and estimated flags, and its input and output item streams
  in units per second — choice-slot draws spread over a recipe's variant
  lines by run share; `items` is the same display lookup the
  other endpoints ship. Shape errors (no targets, an unknown kind or
  objective, a non-positive rate, an out-of-range tier) are 400s; everything
  the solve itself diagnoses answers 200 with a `status` and structured
  `warnings`. The `factoryId` is a content hash of everything that shapes
  the plan — garage, `b`, weights, targets, priority order, **pins**, and
  the mob-farms and bred-seeds toggles (§5: the cost-solve key excludes
  pins; this one cannot). An optional `steps` list — `[{id, machineItemId,
  ocSteps}]`, each id a recipe or a generator line id from an earlier
  plan's lines — turns the solve into a **pipeline**: the candidate set is
  exactly the steps, with no walk, no cost-band or generator pruning, and
  no scope gate (an explicit step is its own consent; the toggles keep
  gating only the mob-drop seed set), though never past garage or
  environment legality — an illegal step warns `step_illegal` and drops
  out, an id naming nothing warns `step_unknown`. Whatever no step makes
  is supplied from outside at its **standing price** — the cost table's
  price for it, seeds still free — so a half-built pipeline solves and
  shows its open inputs; a produce target is never supplied, and one no
  step makes reads `unreachable_target`. A step's `machineItemId` and
  `ocSteps` narrow the recipe's run variants to the pinned block and
  overclock; a pin no buildable variant satisfies warns
  `step_variant_unknown` and falls back to the free choice. An optional
  `supplies` list of item ids declares free sources: each buys at zero
  instead of its standing price, even where a step also makes it — the
  user's world provides it — though a produce target still never buys;
  an id naming nothing warns `supply_unknown`. Supplies alone (no steps)
  already make the solve a pipeline. A pipeline ignores `pins` — the
  steps are the pins — and its `factoryId` hashes the steps and supplies
  in their place. An inflow's auto-infinite mark means the purchase
  itself was free, never that the item is merely derivable from
  renewables. Entries run single-flight per id and live in the
  same two cache tiers under `craftiger:{schema}:{pack}:{build_id}:factory:
  {factoryId}`, the value a versioned Brotli-compressed plan naming the
  build it was solved on — recomputed, never served, on any mismatch. A
  `timed_out` or `failed` plan is answered but never cached, so a retry
  starts over; the wall-clock budget per solve is configuration.
- `POST /api/factory/generators` — body `{garage, b, weights}` → every
  buildable generator line, unpruned — the id a pipeline step names, its
  map, block, fuel, tier, net EU/t and fuel rate — plus the display
  lookup for the blocks and fuels; feeds the Planner's generator picker.
- `POST /api/factory/producers` — body `{garage, b, weights, itemId}` →
  the item detail with every garage-legal producer across all recipe
  scopes, each factory-only row labeled by its `scope` (`factory`,
  `factory_mob`, `factory_bred`; null is a crafting recipe) and each row
  naming its `machineItemId` — the cheapest block that runs it, for the
  picker's machine icon; the pipeline picker's source, where farm rows
  the crafting tab hides become placeable steps.
- `GET /api/meta` → tier ladder, tier voltages (EU/t per amp per tier,
  indexed like the ladder), machine list (each with its availability
  era), coil list, pack version, atlas dimensions
- Static: `/atlas.webp`, `/atlas-offsets.json`
- Probes: `GET /livez`, `GET /readyz` — bare health checks; the eager artifact
  load at startup means a live process is also ready.

### localStorage keys

`gtnhp.cart`, `gtnhp.pins`, `gtnhp.weights`, `gtnhp.machines` (default tier,
overrides, per-map coils, built multiblocks), `gtnhp.config` (B), `gtnhp.ui`
(display caches plus the applied solve, so an unchanged reload resumes on the
cached solve instead of asking for a recalculation), `gtnhp.factory` (the
factory targets, scope toggles and layer priority; the plan itself stays in
memory — a reload starts idle), `gtnhp.planner` (the grid's nodes with
their positions, rates and locks; a stored step-and-target list from the
pre-grid tab is migrated into nodes on load, and the live loop re-solves
once the grid holds a target and a feed), `gtnhp.rateUnit`, and the layout
preferences `gtnhp.sidebarWidth`, `gtnhp.sidebarHidden`, and
`gtnhp.chainOrientation` (shared by both flow graphs). Machine keys inside
`gtnhp.machines` follow the
artifact's renames on load, so a stored tier or coil outlives a rebuild that
renamed its map.

## 9. Exclusions, non-goals, and risks

All "does not / never" rules live here; other sections only reference this one.

### Excluded by design

- **Crafting-tree and step-list views** — the result is always the flat BOM grid.
- **Energy and machine time in prices** — cost is material-only.
- **A solved table never outlives its artifact build** — the store's key and
  the stored value both name the build, and an entry from any other build, or
  one the reader cannot parse, is recomputed, never served.
- **Catalyst costs** — tool, mold, and circuit slots (§3 step 2) never price
  and never gate eras: a mortar survives its crafts, so charging one per run
  would overprice every hand-ground dust. They ship as `catalyst`-flagged
  `recipe_inputs` rows; the solver reads only whether a slot holds a wearing
  tool, to break exact ties (§5), never what it costs.
- **Recipes that consume nothing** — a recipe whose every input is a
  catalyst conjures its outputs, and a material-only cost has nothing to
  say about it: such a recipe never prices, in the shipped engine and in the
  builder's own price check alike. The Tree Growth Simulator is the one
  such recipe family that ships (§3 step 2): it exists for rate planning,
  where a run costs machine time and EU, while its logs keep pricing at
  their farm-leaf weight. A pin may still choose it for a BOM, which then
  makes the logs for nothing. Its tool multipliers are GT5-Unofficial's
  own: the exporter probes every item through the controller's tool check,
  so the dump lists exactly what the machine accepts per mode (logs by Saw
  ×1 up to Chainsaw ×4; saplings by Branch Cutter ×1, grafters ×4; leaves
  by shears ×1 up to electric Wire Cutter ×4; fruit by Knife ×1), and the
  shipped catalyst slot holds only the
  best. Electric tools recharge externally and wearing ones survive the
  run, so none is consumed. The hatch tier's yield — 2t² − 2t + 5 — is
  what the controller computes from one energy hatch; that two ordinary
  hatches read as the next tier is the same amperage lift rate planning
  ignores on every multiblock, so a tree farm climbs exactly the garage's
  tiers.
- **Byproduct credit** — sibling outputs never reduce a recipe's cost;
  crediting them collapses all prices toward zero through recycling loops.
- **Factory-scoped rows** — recipes marked with a `scope` never price (a
  water-fed farm would collapse crafting prices), never date eras (a
  catalyst seed gates no era), and never appear in the crafting tab; only
  the factory solve reads them, `FACTORY_MOB` rows only with the
  mob-farms toggle on, and `FACTORY_BRED` rows only with the bred-seeds
  toggle on — bred rows dominate their fresh twins at no extra input, so
  admitting them freely would decide the assumption for the user. A
  pipeline step names any scoped row directly: the explicit choice is the
  consent the toggle would otherwise give.
- **Pipelines never expand** — a pipeline solve (§8 `steps`) never reads
  pins, never adds a recipe beyond its steps, and never crafts a missing
  input implicitly: what no step makes is supplied at its standing price,
  visibly, and a produce target is never supplied at all — not even one
  declared in `supplies` — a pipeline that cannot make its target is
  infeasible, not quietly outsourced.
- **Pseudo-recipe sources** — bee breeding, dungeon/chest loot, and GT
  informational tabs (material lists); the builder drops them
  (§3 step 3) because they conjure matter from nothing and poison
  prices. Mob drops get no *priced* edges either: their Extreme Entity
  Crusher rows are factory-scoped, so the cost engine keeps buying mob
  drops at their weights — an ore-from-drill recipe forms an amplifying cycle that spirals
  every cost to zero. Fuel tabs need no list at all: a map whose backend
  burns fuels for EU carries the dump's fuel flag, which covers every
  generator from the Gas Turbine to all five Naquadah reactor tiers. Mining maps that output ore blocks from real equipment
  (Space Mining) are *era-only*: they gate progression in the era fixpoint
  (§3 step 6) but never reach `planner.sqlite`, so they can never price. The
  Heliofusion Exoticizer is era-only for the same reason the Replicator is
  excluded: a star turning plasma into magmatter is priced in EU this model
  refuses to count, and its matter cost alone (1.83 per magmatter ingot
  against a tier-13 leaf weight) is fiction — so magmatter and quark gluon
  plasma gate at the Godforge's era while magmatter's ingots and dusts price
  as tier-13 leaves. The
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
- **Recycling a manufactured item** — GregTech decides what reverse-crafting
  gives back from an item's material composition, not from the recipe that built
  it. Where the pack also sells a cheaper crafting recipe, the round trip returns
  more than it consumed: an iron door costs four plates and arc-furnaces into six
  ingots, and that loop alone drives iron, and everything built from iron, toward
  zero. GregTech tags such recipes with a category ending in `recycling`, but the
  tag alone condemns nothing — it applies just as readily to melting a rod back
  into molten metal, which is exactly conservative and is often the only route to
  a material's molten form. The two are told apart by what is consumed: a tagged
  recipe survives when every ingredient is one shape of a single material, and
  is dropped when anything else goes in — a `doorIron`, a `signWood`, an
  Electric Piston with no oredict at all. GT's own prefix flags decide what a
  shape is: an oredict whose exact prefix is unifiable, material-based, and
  not a container (`ingot*`, `plate*`, `wireGt*`, `toolHeadDrill*`, …, or any
  fluid) qualifies, while a `cell*` holds its material inside a container item
  and never does. Storage-block cycles are safe either way: nine ingots in,
  nine ingots back. A few reverse-crafting recipes carry no
  recycling tag at all: Mining Pipes multiply matter on the way out (one fluid
  pipe extrudes into up to 32 of them as a deliberate in-game cheapening), so
  grinding or arcing them back to steel amplifies — one stainless ingot would
  return sixteen ingots of steel and drag the whole ferrous family onto that
  loop's fixpoint. These are caught by matter conservation, with no list to
  maintain: a recipe that takes exactly one kind of item — not itself a
  material shape — apart into nothing but material shapes is a claim about
  how much matter that item holds, and the claim is checked against GT's own
  composition record (`GREG_TECH_ITEM_DATA`, byproducts included, the
  per-item truth outranking the prefix default — a quartz block holds four
  gems, not nine ingots) or, for an unrecorded item, against every fully
  accountable recipe producing it — when every such route puts less total
  matter into the item than the recipe hands out, the recipe amplifies and
  drops. Conservation is of volume, never of identity: GT transmutes freely —
  alloying mixes new materials, implosion leaves ashes, granite grinds to
  thorium — so outputs may be materials the inputs never contained, as long
  as the total adds up. Recipes mixing several ingredients are production,
  however lopsided the matter: the primitive blast furnace really does boost
  two dusts and coke into three ingots. Everything unprovable is innocent
  and stays: world-obtained inputs (cobblestone really does grind to stone
  dust, whatever the rock breaker paid for it), containers, farmables,
  fluids, compositions with an undefined amount (−1 is unknown, never zero),
  and items no accountable recipe produces (mob drops, bee products). Exact
  reverse-crafts survive too — a casing that grinds back to precisely the
  metal it was rolled from is honest — and crafting-grid consumers (the Block
  Breaker really is built from a Mining Pipe) are untouched either way.
- **Phantom registrations** — a recipe the game registers but the machine never
  performs is excluded by its dump id in `PhantomRecipeIds`, each entry carrying
  the in-game observation that condemned it. The one known case: the canner
  empties the BartWorks Iodine Cell into Iodine, yet the registration says
  Molten Iodine — a fluid seven times as dense in matter, which would mint
  seven dusts from one on every round trip. In-game verification is the bar
  for this list: a recipe merely looking wrong is not enough.
- **Coil checks on the Godforge** — the Helioflux Melting Core smelts with the
  star's heat, not coils, so its recipes never check heat: owning the module is
  the only gate. Its recipes carry the map's heat values all the same, and its
  hottest smelts (the 100,000-heat Stargate crystals) are exactly the ones no
  coil ladder could ever reach.
- **Pins that close loops** — a pin may point an item at a recipe whose chain
  leads back to the item itself (pack the block, then break the block). The
  BOM walk needs a DAG, so such a pin is ignored with a warning, exactly like
  a pin the garage cannot run.
- **Taking assembled items apart** — the Unpackager reverses packing and
  assembly, which is not how the parts are made and hands them out a tier early:
  unpackaging a T4 fluid cell yields the Neutronium frame inside it, which
  arc-furnaces into ingots long before neutronium is otherwise reachable.
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
20. Reverse-crafting a manufactured item never ships, while melting one shape of
    a material into another does, and the build reports no leaf priced below a
    millionth of its own weight — the gap between a genuinely cheap route and a
    loop that creates matter.
21. An Electric Blast Furnace recipe just above the installed coil's heat
    becomes legal one hatch tier later; on the other coil maps it never does.
22. A pin that would make the BOM walk loop is ignored with a warning, and the
    result matches the unpinned plan.
23. Where recipes tie for an item, the plan lands on the best-ranked material
    form — ingot before dust before nugget and piles — unless the reroute
    would close a loop, and a real price gap in a worse form's favor still
    wins.
24. Where forms tie too, the shallower route wins — melting an ingot beats
    melting a rod made from it — and among cost-tied leaves the lower-weight
    material wins, so plain steel beats magnetic steel.
25. The whole-run plan never contains a partial craft: one clay ball plans a
    whole block broken (a quarter expected), an exactly-divisible demand adds
    no extra run, and two consumers of half a batch each share one whole
    batch.
26. A default-tier garage does not run recipes on machines first craftable
    beyond that tier — an HV default never plans the Circuit Assembly Line —
    while an explicit per-machine tier brings them back.
27. Grinding an ingot to dust shows the mortar in the recipe, and the dust's
    price does not contain the mortar's.
28. Macerating or arcing Mining Pipes back to steel never ships, so no ferrous
    price rides the pipes-per-ingot amplifier, while the Block Breaker still
    crafts from a Mining Pipe.
29. Two forms at an exact price plateau never point at each other: the dust
    keeps its real producer even when macerating the ingot ties the price
    with a better-ranked form.
30. Unification follows GT's own verdicts: a name GT unifies merges to GT's
    target item, a blacklisted member keeps its identity as a slot
    alternative, a name GT does not unify never merges, and a convention
    name that merely starts with a material prefix classifies nothing.
31. Material shapes follow GT's own prefix flags: recycling a wire survives
    as one shape of its material, recycling a cell never ships, and no
    config list names the shapes.
32. A gem grade prices by GT's material amounts — a chipped gem a quarter of
    its gem, an exquisite four times — shipping as a fraction whose parent
    is the gem.
33. Fuel tabs and steam relaxation follow the dump's flags: no fuel map's
    tab ships as a recipe, and a steam machine runs its map's LV recipes in
    the steam era with no voltage floor, whatever the machine is named.
34. A crafting tool of any mod ships as a catalyst through its container
    item, a bucket-returning item stays an ingredient, and no prefix list
    names either.
35. Matter conservation is derived, not configured, and counts volume, not
    identity: the Mining Pipe grinds drop with no list naming them, exact
    grinds and grinds of unproducible or world-minable items survive, GT's
    composition record bounds an untagged grind with byproducts counted,
    and an undefined amount is unknown, never zero.
36. A recipe whose oredict slot lists its own output — the circuit wrapper, a
    GT dye crafted from its own dye group — prices from the other alternative
    and its BOM expands to that alternative, never looping back once the two
    tie.
37. A loop that consumes an outside input — nine parts per chip, one part and
    europium per chip — plans as the summed series in both accountings and is
    seeded once through the cheapest outside route, the seed's unit counting
    toward the demand so a single chip is the seed route alone; a loop nothing
    outside produces keeps its totals and warns; a pin that builds a loop with
    no finite plan is still ignored.
38. An item nothing in the pack produces and that is not a raw material reads
    `uncraftable` under every garage, while a craftable item that is merely
    unpriced at the garage keeps its `∞`.
39. Search matches a substring of a name or alias regardless of letter case
    on every script — an uppercase query finds a lowercase non-ASCII name and
    vice versa — both through the trigram index and on the short-query scan,
    and lists the cheapest matches first, then by name.
40. A solve computed by one process is served by another from the store
    without recomputing; an entry stored for a different artifact build, or
    unreadable bytes, are recomputed rather than served; a process evicting
    an entry from memory still answers for it from the store.
41. Where a hand craft that wears a tool ties a machine recipe exactly, the
    plan lands on the route with fewer tool slots — the frame box assembles
    rather than wrenches — while a tool route that is genuinely cheaper, or
    shallower, keeps winning; only a wearing tool carries the flag, never a
    circuit, mold or shape, and a tool slot's price is still nothing.
42. A shaped crafting recipe ships its shape — the frame box's wrench in the
    centre cell and the rods around it, a choice cell pointing at its choice
    slot — and every cell points at a slot the recipe actually ships; a
    shapeless recipe and a machine recipe ship no shape, and the chain card
    and item detail draw a shaped recipe on the 3×3 grid, folded otherwise.
43. Every item ships its stack size and fluids none; a whole amount of a
    stackable item reads as stacks in the slot tooltip — 335 of a 64-stack
    item as `5×64 + 15`, exact stacks without a remainder — while a fluid,
    a catalyst slot, an unstackable item and an amount under one stack show
    no stack line.
44. Every recipe carries its amperage and its cleanroom and low-gravity
    flags: a thermal-centrifuge-class 2 A recipe ships `amps = 2`, a 1 A
    recipe `amps = 1`, and a flagged recipe ships both requirement columns
    set while an unflagged one ships neither.
45. Fuels normalize per family: a benzene cell lands as the benzene fluid
    at 360 EU/mB, a cell's special value reads per mB no matter the cell's
    volume, a bare solid burns as 1000 mB worth, an RTG pellet carries its
    EU/t over a year's ticks per burn-year unit, and a GoodGenerator
    naquadah fuel carries its special value as EU/t over its burn ticks
    with the spent fluid it returns.
46. Large-boiler burn times parse per generation from the dump's burn-time
    text, and a "Not allowed" generation ships no row.
47. Machine props merge per machine block: a generator block ships its
    efficiency and output, a dynamo hatch its voltage × amps capacity, a
    boiler its EU/t rating, a bonus-bearing multiblock its parallels and
    typed bonus rows with their scaling axes, and a rotor its per-fuel
    tight and loose stats.
48. Auto-infinite seeds mark their source kind: water is a `WORLD` seed, a
    crop drop or farmable a `FARM` seed and a log never, a capturable mob's
    drop a `MOB` seed, an uncapturable mob's drop no seed at all — and
    `tier_voltages` ships beside `tier_names`.
49. A Tree Growth Simulator row ships once, at LV on a five-second run, with
    its sapling and each output class's best tools as catalyst slots and
    the chainsaw-class multiplier in its amounts; the sapling it grows is
    no longer a `FARM` seed; the recipe never prices, so the log keeps its
    leaf weight; and a factory line on it multiplies outputs by the hatch
    tier's yield at unchanged duration, on a real block and on the
    anonymous fallback alike.
50. A dated mob's drops seed the era solve at the mob's era — a relic
    smelted from an MV-dated boss's drop tiers at MV — while an unlisted
    mob's drop seeds nothing and the same ingot only gets its recipe-tier
    fallback.
51. A factory running a cleanroom-flagged recipe carries one hosting
    Cleanroom line at the garage-tier draw beside its machines; a garage
    below the cleanroom or low-gravity era wall cannot plan a flagged
    recipe at all and the target diagnoses as unreachable.
52. A CropsNH crop ships one factory-scoped row per Crop Manager tier and
    per Industrial Farm seed-bed tier — field-sized amounts, water per
    maturation, fertilizer only from crop tier 9 — and no engine but the
    factory solve ever reads them.
53. A soul-vial-capturable mob ships one `FACTORY_MOB` crusher row at
    1920 EU/t whose duration follows its health; the mob-farms toggle
    gates the row, and an uncapturable mob ships nothing.
54. Only WORLD seeds ship: a crop drop or farmable prices at its leaf
    weight wherever no farm line is legal, and turns auto-infinite where
    one is.
55. A combustion engine burns by integer division — 512-EU diesel draws
    3 L/t unboosted for the nominal output — and its boosted line triples
    output for the booster gas, doubled fuel with the weighted top-up on
    over-rich fuels, and doubled lubricant; a fuel richer than the nominal
    output ships only the boosted line.
56. A reactor line ships per coolant-excited combination — coolant
    multiplies output alone, an excited liquid output and fuel together,
    over the flat upkeep — returns its spent fluid, and skips every
    combination whose full output no buildable dynamo hatch covers.
57. Crop rows bake fresh 1/1 stats — a tier-1 crop rates 12 unfertilized —
    and below the tier-9 wall a fertilized twin grows faster for the
    fertilizer the machine spends: choice-slot items on a manager, liquid
    potency on a farm.
58. Industrial Farm builds bake their units: the all-accelerator build
    divides duration by one plus the slot count at its multiplied power,
    the harvest build always drinks enriched liquid fertilizer, and only
    the overclocked build climbs the standard ladder.
59. A bred row waits for the bred-seeds toggle and rates a 31/31 seed —
    64 growth points against a fresh seed's 12 on a tier-1 crop — and a
    factory-scoped row never lists among an item's crafting recipes.
60. `/api/factory/solve` plans machine lines for a produce target, the
    per-line EU/t sums to the plan's draw, and the response ships the
    display lookup for every item the plan names.
61. Identical factory requests land on one `factoryId`; changing a pin or
    a scope toggle lands on another — unlike the cost `solveId`, which
    ignores pins.
62. A replica serves a stored factory plan without re-solving, and a plan
    from another artifact build decodes to nothing and is recomputed.
63. A factory request with no targets, an unknown kind or a non-positive
    rate is a 400; an energy target no legal generator can serve answers
    `infeasible` with a `no_generator` warning, and `/api/meta` ships the
    tier voltages.
64. The machine closure stops at leaf-class items unless `deep=true` walks
    through them: a recipe-producible leaf lists its machines only on the
    deep walk.
65. A pipeline runs only its steps: a step forces a route the free solve
    would never pick, and an unknown step id warns without stopping the
    rest.
66. A missing pipeline input is supplied at its standing price — a
    non-leaf intermediate at what its chain would cost — while a produce
    target no step makes is `unreachable_target`, never supplied.
67. A step's pin holds the overclock, an impossible pin falls back with
    `step_variant_unknown`, and a generator step selects its line past
    the catalog's pruning.
68. Steps hash into the `factoryId` in place of the pins a pipeline
    ignores: adding a pin changes nothing, changing a step's overclock
    changes the id.
69. `/api/factory/generators` lists a buildable line with its net EU/t
    and fuel rate; an energy target demanding a tier above every line
    still answers `no_generator`.
70. A declared supply buys free — it undercuts even the step making it —
    an unknown supply id warns `supply_unknown`, a supply never covers a
    produce target, and supplies hash into the `factoryId` and make a
    stepless request a pipeline.
71. `/api/factory/producers` lists a farm-scoped producer with its scope
    label beside the crafting rows the item detail alone would show, and
    every row names the machine block that runs it.
72. The coil ladder derives from the dump and the era solve: names and
    heats are the coil export's, each tier is the coil item's own solved
    era, and a coil first craftable only through another coil's heat gate
    settles on a later solve pass (Kanthal made from EBF aluminium tiers
    above Cupronickel).
