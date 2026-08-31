# Factory — design proposal

Status: **draft, revision 3 — all design questions resolved**. Nothing here
is implemented; `spec.md` stays the source of truth, and on approval the
accepted parts of this document merge into it (new sections plus deltas to
§2, §3, §7, §8, §9, §10) in the same commits as the code. The feature is named **Factory** (the new
tab); the existing planner tab is renamed **Crafting**. Internal names follow:
`/api/factory/solve`, `factoryId`, `gtnhp.factory`.

## 1. Statement of value

As a user of the GTNH planner, I want to plan automation pipelines — "I want
resource X at rate Y" — with the best routes available at my current era.
Examples:

1. Produce 32 Polyethylene ingots per minute at HV, using the most
   auto-infinite (renewable) resources possible.
2. Produce 8A HV worth of energy (8 × 512 = 4096 EU/t) by burning logs in a
   Pyrolyse Oven for benzene and burning the benzene in gas turbines.
3. Fully process 64 ore blocks at a rate of 64 blocks / 32 s with the most
   efficiency (byproducts recovered, nothing wasted).

The planner answers with a steady-state flow plan: which recipes run on which
machines, how many of each, at what overclock and parallelism, what flows on
every edge per second, what leaf resources are consumed per second, and which
of those inflows are auto-infinite.

## 2. Why the cost planner cannot do this

The existing solve prices *amounts*; a factory balances *rates*:

- **Time and power are first-class.** The cost model ignores
  `duration_ticks`/`eu_t` entirely; a factory plan is defined by them.
- **One best recipe vs. a mix.** The cost engine picks a single `bestRecipe`
  per item; an optimal factory may legitimately split an item across several
  producers, and may run a recipe for its byproduct.
- **Cycles are the point.** The cost engine's acyclicity invariant
  (strict-improvement updates, spec §5) exists precisely to break loops; a
  steady-state factory *wants* loops (solvent recycling, oxygen loops) and
  handles them natively as balanced flows.
- **Byproducts matter.** "No byproduct credit" (spec §9) is a *pricing* rule
  and stays untouched for the cost engine. In a factory, routing byproducts
  back into the network is the whole job — example 3 is nothing else. The §9
  rule is re-scoped to pricing, not excepted (§11).

Reused: the artifact, the garage and garage-legality (multiblock hatch tiers,
coils, heat), upstream closure, leaf classification and leaf weights, and the
chain-graph UI machinery (via an adapter, §6.4). Pins are reused with changed
cache semantics (§4.1, §6.2) — unlike cost solves, a pin changes the LP.

## 3. Prior art

Three community tools independently converged on this design's architecture:

- **[gtnh-flow](https://github.com/OrderedSet86/gtnh-flow)** (OrderedSet86) —
  hand-written YAML per factory, balanced by solving a system of linear
  *equations* (sympy `linsolve`) with no objective — which is exactly why it
  fails on shared ingredients: equality systems cannot express choice or
  slack (the README's workaround is renaming `chlorine 1`/`chlorine 2`). Its
  curated data files (`overclock_data.yaml` with per-multi
  `[speed, EU discount, parallels]`, `power_data.yaml` generator
  efficiencies, `turbine_data.yaml` rotor stats) define the community-standard
  shape for machine-property data — and their own comments admit staleness
  ("last updated 2.2.3"), which is the argument for extraction over curation.
  Its `projects/renewables/` catalog (~130 charts: hydrogen from water,
  nitrogen from air, oxygen, carbon, aluminium, …) independently matches the
  auto-infinite concept of §4.6 and serves as the seed-list checklist and
  test-case source.
- **flow2** (OrderedSet86's actual v2; `gtnh-flow-v2` forks mirror v1) —
  research notes prove single-objective LP fails both ways (min-quantity
  degeneracy, reward-flow unboundedness) and land on a **lexicographic chain
  of LP/MILP solves**; benchmarks found **HiGHS the only backend fast enough**
  (worst case 1.4 s on a 56-machine line; CBC DNFs and returns
  conservation-violating solutions). Its last stage — minimize total flow to
  canonicalize degenerate optima — is adopted here (§4.3), as are its LP
  engineering traps (§6.1) and its diagnostics rule: *never a question before
  a solution*.
- **[gtnh-factory-flow](https://github.com/jackwrichards/gtnh-factory-flow)**
  (gtnhplanner.com) — manual React-Flow machine graphs over a dataset exported
  by a custom Forge mod running inside GTNH (validating both the offline
  artifact and the exporter-extension path). Its iterative fixed-point
  balancer accreted special cases and documented gridlock failures; it is
  being replaced by a lexicographic chain of LP solves. Two hard lessons
  adopted here: **tooltip scraping of multiblock bonuses produced
  plausible-but-wrong values** and was replaced by a curated table audited
  against GT5-Unofficial source; and **hand-curated fuel values drift** (it
  carries benzene at 32,000 EU/L against the true 360 EU/mB) — fuel values
  must be extracted, never hand-maintained.

Craftiger's edge: recipes, byproducts, durations, machine properties, and era
legality all come from the artifact, and route *selection* is part of the
optimization under garage legality — no YAML, no hand-wiring.

## 4. Model

### 4.1 Flow network

Over the candidate set (§4.2) of garage-legal recipes:

- **Variable** `x_{r,m,k} ≥ 0` for recipe `r` on eligible machine block `m`
  at overclock step `k`: runs per second, continuous. Machine choice is
  *inside* the LP: one map is served by blocks with different properties (the
  blastfurnace map alone: EBF with heat OCs, Volcanus with 8 parallels / 220 %
  speed / 90 % EU / 10 L/s pyrotheum, Mega EBF with 256 parallels), and they
  trade off differently under each objective — a pre-pass cannot resolve it.
  Overclock step `k = 0 … (effectiveTier(m) − tier(r))` is likewise a solver
  choice (§4.4). Below, `x_r` abbreviates the sum over a recipe's columns.
- **Balance** per item `i`: `net(i) = Σ x · out(i) · chance − Σ x · in(i)`,
  per second, expected value on chanced outputs (consistent with spec §4).
- **Leaves** get an explicit purchase variable `buy_l ≥ 0` with
  `net(l) + buy_l ≥ 0`; objectives price `Σ weight(l) · buy_l`. This is
  deliberate: pricing raw `−net(l)` would grant full-price byproduct credit
  to *unconsumed* surplus of leaf-class items (every dust and ingot is one)
  and can make the objective unbounded. Consuming a byproduct inside the plan
  is flow and offsets purchases; overproducing it is surplus, never credit.
- **Non-leaf items**: `net(i) ≥ 0`; positive residual is reported as
  **surplus**, never silently discarded.
- **Catalysts and tools** do not flow (consistent with pricing); they are
  listed once per machine line as setup requirements. Continuous auxiliary
  draws that are *not* recipe inputs (Volcanus: 10 L/s Blazing Pyrotheum
  while running) DO flow — they ship in `machine_props` (§5.2) and enter the
  balance as `x · duration_eff · rate`.
- **Pins** never bypass garage legality. Flow semantics (the cost engine's
  "one bestRecipe pointer" does not transfer): a pinned item forces `x = 0`
  on every recipe that lists the item as a **deterministic output**
  (`chance = 1`) other than the pinned recipe; chanced byproduct rows stay
  free, so pinning benzene to pyrolyse does not gut ore processing. A pin
  that makes a target unreachable is diagnosed, not silently absorbed
  (§6.1). Unlike cost solves, pins are part of the factory cache key (§6.2).

### 4.2 Targets and candidate set

A factory is a list of targets; all constrain one network:

- **Produce**: `net(item) ≥ rate`. Candidate set: garage-legal upstream
  closure — but unlike the cost closure, the walk continues **through
  leaf-class items** (their producers are candidates too). A factory may
  produce a leaf — every ingot is one, including example 1's Polyethylene
  Bar — and buying is just the competing route the purchase variable prices;
  a closure that stopped at leaves would degenerate example 1 to "buy PE".
- **Consume** (example 3): supply enters as a variable `0 ≤ s ≤ rate` in the
  item's balance; a pre-layer maximizes `s`, and `s < rate` is reported as a
  shortfall naming the blocking item — never a bare infeasibility. Candidate
  set: the *downstream cone* of the consumed item (its consumers,
  recursively) unioned with the upstream closures of those recipes'
  co-inputs.
- **Energy** (example 2): EU is a pseudo-item with a balance row —
  `net(EU) = Σ generator output − Σ machine draw ≥ target` — so the target is
  **net export** after the plan powers itself. Machine draw is the
  duty-cycled expectation `x · duration_eff · eu_t · amps`, linear in `x`;
  never `machines × eu_t` (nonlinear, wrong for part-idle lines). Generator
  pseudo-recipes (§4.5) join the candidate set only when an energy target
  exists. EU quantity is one balance row (any generator can power the plan's
  own draw; transformers are free and lossless), but the exported amperage
  must physically come from generators of sufficient quality: an A×tier
  target adds `Σ output of generators with voltage tier ≥ target tier ≥
  target` — 128 LV turbines never satisfy "8A HV".

Entry is "amount per time window" (`32 / 1 min`, `64 / 32 s`), normalized to
per-second internally; display follows the global unit picker (§6.4).

### 4.3 Objectives

Each factory carries an **objective priority order** — a per-factory picker
over the three layers, default order as listed:

1. **Resource efficiency**: minimize `Σ weight(l) · buy_l`, where
   auto-infinite seed leaves (§4.6) count at weight 0.
2. **EU efficiency**: minimize total machine draw (generators excluded —
   their cost is fuel, already priced in the resource layer through the fuel
   chain).
3. **Machine efficiency**: minimize `Σ x · duration_eff / P` — busy-machine
   time, parallel-adjusted (§4.4).

Consume-target factories run the maximize-`s` pre-layer first — locked
exactly, not by tolerance: the intake is a commitment. The planned
**maximize recovered value** layer (`Σ` over *leaf-class* items of
`weight · surplus`) measured **unbounded on the real artifact in three
formulations** (open maximize after the resource lock; runs-locked; cone- and
buy-restricted with capped surroundings): the leaf weights are not
arbitrage-free and free world-origin chains exist by design, so any open
value-maximize finds a mint — e.g. weight-zero fluids reached by the cone
feed converters into weighted dusts without limit. **Decided: the layer is
dropped for v1** — byproducts still flow, feed downstream consumers, and
surface as surplus, but the solver does not actively prefer byproduct-richer
processing routes (a supplied ore may simply smelt). Steering remains
available through pins and the priority order; an active value-seeking layer
returns only with a weight model that can support it. After all layers, a fixed hidden **canonicalization layer** minimizes
`Σ x_r` (total runs): it removes zero-cost churn cycles (72,594 artifact
recipes have `duration = eu_t = 0` and are invisible to layers 2–3), makes
the returned vertex substantially model-determined rather than
pivot-order-determined (required for caching), and if *it* is unbounded the
model has a genuine free-lunch cycle and the solve fails loudly, reporting
the cycle. A builder test asserts no garage-independent zero-duration
zero-EU cycle is expected-value-multiplying.

The OC trade-off is what the priority picker decides: fewer OC steps halve
power per step but double machine time, so EU-first plans under-clock and
machines-first plans max-clock — both are correct outcomes of §4.1's per-`k`
columns. EBF heat OCs (÷4 duration, ×4 power) are energy-neutral and always
taken.

Mechanics (settled by measurement on the real artifact, phase 2): HiGHS's
native lexicographic mode re-solves lower layers cold and measured minutes
against seconds, so layers run as **sequential solves on one live instance**,
fixing layer `j` as `obj_j ≤ z*_j + max(ε_abs, ε_rel·|z*_j|)` with
`ε_abs = 10⁻⁶, ε_rel = 10⁻³` — tighter corridors broke the simplex numerics
(postsolve solutions landed outside them and feasibility recovery never
converged), and a 0.1 % layer trade is invisible in any displayed plan. Each
layer **clears the solver state first so it presolves from scratch**: a
hot-started basis skips presolve and dual simplex crawls on the full column
space (warm primal starts were measured correct but 30× too slow). The
adapter equilibrates the matrix with exact power-of-two row and column
scales and normalizes each layer's cost vector to unit geometric mean —
coefficient ranges legitimately span chanced yields to `2 × 10⁷` EU/t and
unscaled models broke presolve–postsolve equivalence. The canonicalization
layer runs **capped to the standing solution** (every column's upper bound
becomes its current value, so churn can only shrink and unused columns stay
unused): its full-space form is a maximally-degenerate all-ones objective
that measured minutes, and fixing zero columns outright fails — solver-space
dust on the wide-coefficient lock rows is macroscopic, and discarding it
makes presolve prove the restricted model infeasible. The same dust bites a
second way once seeds exist: an all-seed plan's resource optimum is exactly
zero, its lock corridor collapses to `ε_abs = 10⁻⁶`, and the restricted
layer's box bounds inherit standing dust down to `10⁻⁹` — bounds presolve
treats as zero, re-fixing the dust columns and proving a tolerance-feasible
model infeasible (measured on the 8A-HV solve: the standing point satisfied
every row to `5×10⁻⁸`, well inside solver tolerance, while presolve still
returned Infeasible). Cured in the adapter two ways: the box **contains the
standing point as-is** (negative dust included) **with every nonzero side
floored at `10⁻⁶`** so dust columns stay real variables — exact zeros stay
fixed, which is where presolve earns its keep — and each lock row is
**re-bounded to contain the standing point** before the restricted solve, a
guard against tolerance-level lock violations (measured `10⁻¹⁷` here). A
flat `ε_abs = 10⁻³` corridor floor was tried first and rejected: the
machines layer spent the corridor buying phantom priced inflows on
otherwise-free plans. The steam carrier stretched the matrix further
(38,400 L recipe amounts, 144,000 L/s turbine draws) and the restricted
layer failed again with every guard in place — the standing point measured
feasible to `5×10⁻⁸` while even a presolve-off simplex declared
infeasibility — so the adapter now retries a failed restricted layer
without presolve and, when that also fails, **falls back to the prior
layer's optimum**: canonicalization is a tie-break, not a constraint, and
a simplex vertex already carries no free-spinning churn. Layer-tolerance
slivers below 10⁻⁵ runs/s are
reporting noise, not lines. Pin solver settings for determinism
(single-threaded simplex, fixed seed); the whole-solve time budget flows
down the layers as each one's remaining time.

### 4.4 Machines, overclocking, parallels

Per column `(r, m, k)`, effective parameters are computed at model build:

- **Overclock** (verified against GT5U `OverclockCalculator`): per standard
  step, duration ÷2 and power ×4; perfect OC ÷4 and ×4. EBF-style heat:
  `floor(excessHeat/1800)` steps become perfect, and EUt is multiplied by
  `0.95^floor(excessHeat/900)` before OC math; steps are bounded by the
  voltage-tier gap. Coil and hatch-bonus state comes from the existing
  garage.
- **Parallels** `P(m)` and **speed / EU modifiers** come from
  `machine_props` (§5.2) as *(kind, base, per-component-tier scaling)*
  records, resolved against garage state at solve time: constant bonuses
  always apply; coil-scaled ones read the existing per-map coil; other
  scaling axes (solenoid, pipe casing, containment block, turbine tier sum,
  controller-menu settings) are user-configurable per family — v1 ships
  pickers only for the most-used families and adds the rest on demand, with
  unconfigured families falling back to a conservative flagged assumption.
  Machines without extracted bonus data run at `P = 1`, no modifiers —
  a conservative overestimate flagged in the UI (§6.4).
- **Machine count** (display): `ceil(x · duration_eff / P)` per line. The LP
  stays continuous; MILP is out of scope. Machine counts are unbounded —
  per-line count caps were considered and declined.
- **Durationless recipes** (crafting grid, furnace-class: ~72.6 k) contribute
  zero machine time and zero power — free infinite-rate converters, flagged
  per line in the UI; accepted v1 behavior.
- **Recipe requirements**: `REQUIRES_CLEANROOM` (762 recipes) and
  `REQUIRES_LOW_GRAVITY` (7) ship as recipe flags, gated by the era walls
  the artifact carries in `meta.environment` (the controller's solved era
  and the first rocket's era). A plan with running cleanroom lines gets one
  hosting Cleanroom line appended after the solve — a fixed charge is not
  LP-expressible, and one warm room is noise against factory scale — at
  the steady draw of `MTECleanroom`'s 40 EU/t recipe overclocked by a
  hatch of the garage's tier, over ten: 4 EU/t through MV, quadrupling
  per tier above. Low gravity is a place, not a machine: era wall only.

Machine construction is free and instantaneous in the model (multiblock
casings and coils are among the pack's most expensive crafts — a real
assumption, stated in §8); a "price this plan's machine list in Crafting"
handoff is the natural cross-tab follow-up (v2, noted in phasing).

### 4.5 Power and generators

Generator properties belong to the machine **item**, not the map: the
Combustion map is served by five single-blocks at 95/90/85/65/50 %
efficiency plus two multis. Fuel maps ship no ordinary recipes, so the
builder synthesizes **generator pseudo-recipes** per (generator item, fuel):

- **Single-block generators** (verified against GT5U `MTEBasicGenerator`):
  EU per mB = `fuelValue(EU/mB) × efficiency/100`; the machine outputs 1A of
  its tier. All 34 single-block generator efficiencies are extractable from
  the dump (`Fuel Efficiency: N%` tooltip lines, clean of formatting codes).
- **Large turbines**: one pseudo-recipe per (turbine, rotor material, fit),
  pinned at that rotor's **optimal flow** — off-optimal is strictly worse and
  excluded from v1. Per-rotor stats (base efficiency, optimal flow and EU/t
  per Steam/Gas/Plasma, loose-fit rows, overflow tier) are fully parseable
  from the dump: ~740 `gt.metatool.01` rotor variants each carry the
  complete stat table in `ITEM_TOOLTIP`, material identity in item NBT.
  Each turbine line defaults to the best garage-legal rotor with a
  per-turbine user override, and tight vs. loose fit is a user toggle per
  line. Rotor wear is ignored in v1 (GT5U's expected durability loss
  `min(EUt/5, EUt^0.6)/2000` per tick would give a rotors-consumed rate if
  ever wanted). The Large Steam Turbine returns 1 L distilled water per
  160 L steam.

  Shipped mechanics (gas and plasma; steam rides the steam carrier):
  requires schema v10 — `machine_items.era` closes the era gap so turbine
  controllers gate on their own craftability, deprecated superseded
  controllers (GT's rigid `§4DEPRECATED` banner, 76 blocks) never ship, and
  a curated overlay flags the rotor-driven controllers (`rotor_turbine` —
  the dump cannot tell a Large Gas Turbine from the Universal Chemical Fuel
  Engine on the same map) and the XL turbos' 16× throughput. One generator
  column per (controller, frontier rotor, fit): craftable = priced by the
  cost table, and the Pareto frontier on (efficiency, output) is judged
  **under the dynamo cap** — measured on the real artifact, raw stats let
  monster endgame rotors (~896k EU/t optimal) dominate every frontier, and
  capped to an HV hatch they burn ~50,000 mB/s for 2,032 net EU/t, so every
  turbine variant priced 1,760× off the pool optimum and pruning erased the
  feature; capped judging keeps the modest high-efficiency rotors (51
  variants survive at the HV bench garage). Each variant picks the hatch
  that nets the most — capacity caps with voiding, the per-amp Enet loss
  rises with hatch tier, amps ≤ 4 for large turbines and unrestricted for
  the multi-amp XLs — and the fit enters as two competing variants until
  the per-line user toggle pins one.
- **RTGs and naquadah reactors**: RTG pellets burn at a fixed EU/t (the
  recipe's VOLTAGE column) for a fixed lifetime — the special value is the
  burn time in years (Po Pellet: 1 → 365 Minecraft days at 480 EU/t) — so a
  pellet is a consumed input at `1/lifetime` per running RTG.

  Shipped mechanics (Large Naquadah Reactor, `MTELargeNaquadahReactor` and
  the pack's GoodGenerator config): each timed fuel's special value **is**
  the EU/t (MkI: 975,000 over 60 ticks per mB), and every burn returns
  1 mB of the re-refinable depleted fluid. One line ships per
  coolant-excited combination: a coolant multiplies output alone (IC2
  105 %, Super 150 %, Cryotheum 275 % at 1,000 L/s; Tachyon Rich Temporal
  Fluid 500 % at 20 L/s), an excited liquid multiplies output and fuel
  together (Caesium ×2, Uranium-235 ×3 at 180 L/s; Naquadah ×4, Atomic
  Separation Catalyst ×16, Spatially Enlarged Fluid ×64 at 20 L/s), and
  2,400 L/s of Liquid Air is drained regardless. The rates and factors ride
  the artifact's `generator_modes` table from the builder overlay, sourced
  from the pack's own `GoodGenerator.cfg`. The reactor stops rather than
  voids on an undersized dynamo and takes multi-amp hatches, so a
  combination whose full output no buildable hatch covers ships no line.
  An unpriced mode fluid does not kill a line: Liquid Air never prices
  (Air is a weightless seed) yet the LP produces it from the free chain.
  The MkI–VI fuels surface automatically once their chains price.
- **Dynamo hatches are capacity constraints, not losses** (verified in GT5U:
  EU is injected 1:1; output beyond total hatch V×A is voided; large
  turbines take one hatch, ≤ 4A): per-generator-line EU/t is capped by the
  best garage-legal hatch under the machine's amperage restriction.
  Additionally (decided) the universal **Enet output loss** —
  `2^max(0, tier−1)` EU per amp emitted (LV 3.03 % … UV 0.024 %,
  code-confirmed) — applies to all generator output; cable losses stay out
  of scope. Maintenance is assumed perfect.
- **Steam is a second energy carrier in v1** (decided), and steam
  generation itself is plannable: boilers produce steam at their
  machine-props yields, era-0 steam machines draw it instead of EU
  (spec §2), and steam turbines convert it. None of those rates exist as
  dump recipes — boiler fuel recipes output *no steam* (per-controller
  yields live in tooltip prose) and the single-block steam turbine's
  conversion ("2 L steam → 1 EU", efficiency, max intake) is tooltip-only —
  so all three ride the exporter extension (§5.3). Where two same-named
  boiler controller generations exist (Large Steel Boiler, Large
  Tungstensteel Boiler), the non-deprecated item is used. The Extreme Heat
  Exchanger is *not* a fuels-table entry: its IS_FUEL map holds real
  two-fluid-in/fluid-out recipes and is routed as an ordinary machine map
  (§5.2).

  Shipped mechanics: the builder **synthesizes the carrier into the
  artifact**, so the cost engine prices steam and the runtime stays pure.
  One recipe per (boiler, fuel) turns the fuel plus water (1 L per 160 L,
  plain water, user-confirmed) into IC2 steam at 2 L per EU of the boiler's
  rate over the extracted burn seconds — which **already carry GT's
  long-burn bonus**: `LargeBoilerFuelBackend` applies
  `max(1, 1+ln(steelSeconds/10)·0.025)` before generating the very fake
  recipes the exporter parsed (verified in source; coal and Block of Coal
  both sit at ratio 1.0, which made the table look linear). Dual-fuel boost
  and circuit throttling stay unmodeled. Machine rows and 0.5-EU/L steam
  pseudo-fuels are synthesized for the single steam turbines (their
  source-normalized efficiencies ride the existing generator path; the
  tooltip's "up to 1552 L/s" differs from the code's 1,493 L/s and the code
  wins) and for the Large/XL Steam Turbines (rotor STEAM class, 16×
  overlay parallels, both steams accepted, 1 L distilled water returned per
  160 L). Steam machines run LV-and-below recipes at zero EU draw: bronze
  at twice the recipe duration and 2 L/EU of rate, high pressure doubling
  rate and speed — the same four liters per recipe EU either way
  (user-confirmed against the 30 EU/t → 1,200 L/s example); the GT++ steam
  multiblocks ride overlay bonuses (8 parallels, 125 % speed, 62.5 % steam,
  identical tooltips verified per item; HP mode unmodeled). IC2 and
  Railcraft steam are interchangeable (user decision): drawing variants
  exist per steam fluid, and the candidate walk seeds the steam fluids in
  whenever legal steam blocks exist, since variant draw is invisible to a
  recipe-input walk. **The EU-efficiency layer is carrier-neutral** (user
  decision): steam draw counts at 0.5 EU per liter there — with steam read
  as free, the layer bought 342 busy machines of free-steam detours on the
  PE example to shave 5 EU/t of electric draw. Steam machines therefore
  surface where they genuinely win — the GT++ multis' eight parallels under
  a machines-first priority — rather than as phantom free energy. Zero-EU
  recipes (boilers) never overclock: there is no power to quadruple. Deferred: steam machines do not yet *extend* garage
  legality — a steam-era garage still cannot reach LV recipes through them
  (spec §2's garage-legal definition would have to change); revisit with
  the steam-era garage work.
- Combustion engines' boosted modes are separate competing lines; warm-up
  ramps are transients and ignored — steady state runs at final efficiency.

  Shipped mechanics (`MTELargeCombustionEngine` / `MTEExtremeCombustionEngine`):
  nominal output 2,048 / 10,900 EU/t on `machine_props.generator_eu_t`;
  fuel per tick is `floor(nominal / fuelValue)` — integer division, so some
  fuels burn above 100 % efficiency — with 1 L of lubricant per 72 ticks.
  A fuel richer than the nominal output refuses to run unboosted. Boosting
  (Oxygen / Liquid Oxygen at 2 L/t) triples output for
  `floor(2·nominal / fuelValue)` plus, on fuels richer than the nominal,
  a random top-up modeled at its expectation
  (`frac(3·nominal / floor(1.5·fuelValue))` — HOG burns 1.6384 L/t), and
  doubles the lubricant. Engines emit through one classic dynamo hatch with
  ordinary voiding. The Universal Chemical Fuel Engine is deferred: its
  promoter-ratio efficiency curve (`1.5·exp(−c·fuel/promoter)`) is not
  LP-expressible and gets its own design pass.

### 4.6 Auto-infinite resources ("renewables")

The user's definition: obtainable from resources that can be created
automatically and infinitely. **Lava is not; mob drops are.** The mechanism
has two parts, and neither is a baked per-item flag:

- **The artifact ships only a curated seed set** of primitives
  (`renewable_seeds`, from a checked-in builder config), decided: start from
  gtnh-flow's renewables catalog **minus lava**; **Air and Cobblestone are
  seeds**; Water is in; **farm-product and mob-drop leaves are not** —
  crops, farmables and mob drops are farm-lined or bought, never free of
  machines, the same ruling that already covers logs; **automated fishing
  and scrapbox loops are not** (v1). Leaf classes are *not* trusted: of
  the 8 `world_fluid` leaves only Water qualifies (the rest are Lava and
  six finite oils); `minable_block` mixes Cobblestone with finite End
  Stone.
- **Derivation falls out of the LP**: only leaf purchases are priced, so
  zero-weighting the seeds makes every garage-legal chain from them
  (distilled water, H₂/O₂ via Electrolyzer, N₂ via Compressor→Canner→
  Centrifuge from air, logs via Tree Growth Simulator — all verified as
  ordinary dump recipes) cost only EU and machines automatically. No derived
  flag is needed for the objective.
- **Badges are computed per solve**: auto-infiniteness of a derived item is
  garage-dependent (nitrogen is auto-infinite only where the air chain is
  legal), so the solver computes the monotone fixpoint over the garage-legal
  closure (an item is auto-infinite iff some legal recipe has all
  non-catalyst inputs auto-infinite) and returns badges in the response; the
  UI never badges from static item data. Decided: **EU counts as free**
  inside the fixpoint, recipes whose only other requirement is a catalyst
  qualify, and the UI label for the concept is **∞**.

Mob drops have no *priced* recipe edges (spec §9 scopes the exclusion):
the factory tab derives them through synthesized EEC lines, and mob
farming stays **optional per factory** — the toggle now gates the EEC
lines instead of a seed set. The single scalar "renewable share" is
dropped as undefined (weighted share is 0-by-construction; raw amounts
mix mB and items) — the UI reports auto-infinite and priced inflows as
separate lists.

Shipped mechanics: the artifact's `renewable_seeds` (WORLD rows only —
Water, Air, Cobblestone and its stone variants) enter the solver as a
`FactorySeedData` input; the mob-farm toggle rides the request and gates
the EEC lines. The fixpoint is a worklist over the
garage-legal recipes — a slot is covered when any alternative is
auto-infinite, and catalyst-only recipes qualify structurally because
catalyst rows and EU never enter the index as slots. Flows and inflows carry
the per-solve `∞` badge, seed purchases charge weight zero in both the
resource layer and the reported inflow cost. Measured effect on example 1:
the PE plan now routes entirely from Apple Wood, Sugar Beet and water at
priced cost ≈ 0, as designed. One deliberate gap: the cost-banded candidate
walk still prices seeds at their leaf weights when pruning routes, so a
chain attractive *only* because its seed is free can be pruned before the LP
sees it — acceptable while seed leaves price near zero; revisit if a
free-seed route visibly goes missing.

The **Tree Growth Simulator** is modeled from its real mechanics, pinned
in GT5-Unofficial's `MTETreeFarm`: a sapling sits in the controller as a
non-consumed catalyst, and a GregTech tool in the input bus selects the
output class and multiplies it — logs via Saw ×1 / Buzzsaw ×2 / Chainsaw
×4, saplings via Branch Cutter ×1 / Grafter ×4, leaves via Shears ×1 / Wire
Cutter ×2 / electric Wire Cutter ×4, fruit via Knife ×1 — with every mode
that has a tool harvesting in the same run. Duration is fixed at 100
ticks; the draw is the practical voltage of the tier the controller reads
from its energy hatch, and every output is further multiplied by that
tier's yield, `2t² − 2t + 5` (5 at LV, 9 at MV, 17 at HV, 29 at EV) — an
output ladder, not a speed ladder. Electric tools are recharged externally
and wearing ones survive the run, so no tool is consumed. The dump's 251
input-less treefarm rows already carry the per-mode base multipliers (logs
and saplings ×5, leaves ×2, fruit ×1); the builder adds the sapling (the
row's controller-slot item) and, per mode, the best-multiplying tools of a
curated table as catalyst slots, and ships the LV amounts with that tool's
multiplier applied. A recipe consuming nothing never prices, so logs keep
their farm-leaf weight in the cost engine; no log is a seed, and what the
farm conjures (saplings, leaves, fruit) stops being one: the factory
derives wood through the farm — machines and EU — wherever the controller
is legal, and buys it at its weight elsewhere. The controller's own era
follows the Nether Star in its Field Generators, which the era solve dates
by the Wither's world once mob drops seed it.
Two shortcuts are deliberate: the controller reads two ordinary energy
hatches as the next tier (each hatch works two amps), the same amperage
lift ignored on every multiblock, so a farm climbs exactly the garage's
tiers; and the choice of tool is not a solver decision — catalysts neither
price nor gate, so a lesser tool never wins.


**Farms as machine lines.** Crop drops, farmables and mob drops cost
machines and EU like everything else; only Water, Air and Cobblestone stay
machine-free. Three machine families are synthesized per-row, all mechanics
pinned in source (CropsNH `MTECropManager`, `MTEIndustrialFarm`,
`TileEntityCropSticks`; GT5-Unofficial `MTEExtremeEntityCrusher`,
`MobHandlerLoader`):

- **Scope**: farm and EEC rows are `FACTORY`-scoped — the cost engine never
  prices them (water at ~0 weight would collapse crafting prices), the era
  solve never reads them (a catalyst seed gates no era, so a tier-16 crop
  would date at LV), and the crafting tab hides them. Only the factory
  solve consumes them; crop and mob drops keep their leaf weights and eras
  everywhere else. EEC rows are `FACTORY_MOB`: additionally zeroed unless
  the request's mob-farms toggle is on.
- **CropsNH growth**, shared by both crop machines: a crop stick ticks
  every 256 game ticks and adds `getGrowthRate` points until
  `GROWTH_DURATION` is reached. Rate = `(6 + growthStat) ×
  (100 + nutrients×5 − 10×cropTier)/100`, capped below by a −4%/point
  deficit penalty; nutrients = 5 base + water/10 (≤10) + fertilizer/10
  (≤10) + 2 sky. A fresh unanalyzed seed carries stats 1/1/1 (the clamp
  floor in `SeedStats`), so the baseline base speed is 7; watered sticks,
  sky, neutral biome → 17 nutrients (85 points); fertilized rows → 27
  (135). Fertilizer is mandatory from crop tier 9 (full speed to tier 13;
  the single tier-16 crop stays unfarmable) and below that wall ships as
  a fertilized twin row (`~f`) competing on speed against plain sticks.
  Expected drops per harvest =
  `DROP_CHANCE × 1.03^gain × Σ(weight/10000 × (stack + (gain+1)/100))`,
  encoded through output chances. Every crop row also ships a
  `FACTORY_BRED` twin (`~b`) at the 31/31 breeding cap behind the
  per-factory bred-seeds toggle — bred rows dominate fresh ones at no
  extra input, so a toggle, not the LP, must own the assumption.
- **Crop Manager** (LV…UMV single blocks, radius `3 + 2×tier`): one row
  per (crop, tier); a run is one maturation wave of the whole field —
  duration `256 × ceil(GROWTH_DURATION/rate)` ticks, outputs × field size
  × `(1 + 0.05×tier)` harvest-round bonus, water per seed per maturation.
  Its `V/8`-per-harvest draw amortizes below 1 EU/t and ships as zero; the
  cost is the field of machines. Rows never overclock — each tier is its
  own exact row. Fertilized rows spend the item fertilizers of the
  choice slot.
- **Industrial Farm** (multiblock; seed-bed tiers MV…UXV): same run
  semantics with the bed tier's field, `(1 + 0.2×tier)` rounds, water
  `≈0.39` potency per seed-cycle, and a continuous `VP[tier]` draw; its
  fertilizer is the CropsNH liquid (1 potency/mB), never items. Beyond
  the bare build, one row ships per upgrade build over the structure's
  `bed tier − 1` unit slots (`MTEIndustrialFarm` and the CropsNH unit
  blocks): the all-accelerator build (`~gau`, each unit +100 % speed and
  +125 % base power), the harvest build (`~hrv`, one fertilizer unit at
  ×1.5 speed, +0.5 rounds, +50 % power on enriched liquid at
  10 potency/mB, up to two advanced harvesting units at +20 % rounds and
  +50 % power each, accelerators on the remaining slots), and from ZPM
  the overclocked build (`~oc`, exclusive with plain accelerators),
  whose row alone rides the standard ladder — the in-game overclock
  doubles output and per-cycle consumables for quadrupled energy, which
  per maturation is exactly duration ÷2 at EU ×4. Multi-amp exotic-hatch
  overclocks past the garage tier stay unmodeled.
- **EEC** (one row per soul-vial-capturable mob with drops): duration
  `max(55, health/9 × 10)` ticks at a flat 1920 EU/t, powered spawner as
  catalyst, expected drops from the dump's probability rows (infernal-only
  drops excluded, looting 0), 120 mB xpjuice per kill. Perfect overclocks
  halve nothing — each step divides duration by four down to the 20-tick
  floor, further steps multiply outputs by four.
- Fertilizer and hydration potencies are config: water 1/mB, distilled
  2/mB, CropsNH fertilizer 100/item, bonemeal 5/item, liquid fertilizer
  1/mB, enriched 10/mB.
- Deliberate baselines: no Weed-EX (weeds only matter above what a field
  holds), manager cadence (50-tick work loop) amortized away. Deferred
  with cause: environmental enhancement units need per-crop liked-biome
  data the dump lacks (an exporter extension, alongside the farmables
  tables); EEC looting needs an `ItemSword`-class weapon no recipe in the
  graph can produce — GT's looting chainsaws and butchery knives fail the
  slot's class check — so the crusher stays at looting 0 until an
  external-assumptions framework exists.

## 5. Data

### 5.1 Verified present (current dump/artifact)

- `recipes.duration_ticks` / `eu_t` (186,439 of 259,049 recipes); full ore
  chains with chanced byproducts (example 3 needs no new data); machine
  eras, multiblock tiers, coils.
- `ITEM_TOOLTIP` carries: all 34 single-block generator efficiencies
  (rigid `Fuel Efficiency: N%` lines); multiblock bonus lines in ~10
  template-generated shapes covering ~87 of 122 parallel-bearing machines,
  plus prose/formula machines (Eye of Harmony, PCB Factory, Compact Fusion,
  Spinmatron, Industrial Sledgehammer, Dangote Distillus, Electric Implosion
  Compressor's per-block table); complete per-rotor turbine stat tables
  (~740 variants); dynamo hatch V/A. GT5U generates these lines from a
  closed set of `GT5U.MBTT.*` lang templates — the basis of §5.3's
  extraction.
- Fuel values: `GREG_TECH_RECIPE.RECIPE_SPECIAL_VALUE` on the 19 `IS_FUEL`
  maps (benzene 360 EU/mB cross-checked against the known in-game value),
  duplicated in `GREG_TECH_RECIPE_METADATA` (`fuel_value`) — a free
  second-channel builder assert.
- Renewability chain anchors as ordinary recipes (§4.6); `MOB_INFO_DROPS`
  with 1,137 probability-weighted rows.

### 5.2 Schema bump (v8 → v9)

- **`recipes.amps`** — from `GREG_TECH_RECIPE.AMPERAGE`, which is
  authoritative and already folds in map-level amperage (the 860 >1A rows
  are exactly Thermal Centrifuge 2A + Crop Synthesizer 3A + Mass
  Fabrication 8A complete); builder asserts recipe-level vs map-level
  consistency as the tripwire for exporter convention drift. `eu_t` stays
  per-amp voltage; draw = `eu_t × amps`.
- **`recipes.cleanroom, low_gravity`** — carried flags (§4.4).
- **`fuels`** — one row per (fuel item/fluid, map), extracted via **three
  paths**: (a) cell input → `FLUID_CONTAINER` → fluid-keyed (covers 5 maps
  fully: Acid, Combustion, Extreme Diesel, Gas Turbine, Plasma); (b) direct
  fluid input via `RECIPE_FLUID_GROUP` (5 maps); (c) item-keyed with no
  container (naquadah bolts, RTG pellets, boiler solids, magic dusts). The
  builder carries a **per-map disposition table** — input style, unit
  regime, one reference assert each — because units are heterogeneous:
  EU/mB maps (benzene 360); Large Boiler solids in coal-equivalents
  (Coal = 1, with burn-times per boiler tier in `ADDITIONAL_INFO` prose);
  RTG — VOLTAGE is the output EU/t and the special value is burn time in
  years (Po Pellet: 1 → 365 Minecraft days at 480 EU/t); GoodGenerator
  naquadah — special value with DURATION gives the NEI base output (§4.5);
  plasma — the special value is **EU per cell**, and a plasma cell holds
  1000 L (user-confirmed; the builder asserts each plasma cell's
  `FLUID_CONTAINER` volume, and the survey's 144 mB reading is re-checked
  there). `semifluidboilerfuels` empty is **correct, not an exporter bug**
  (phase 0 verified): no machine class serves that legacy GT5U map — GTNH's
  semifluid fuels live on GT++'s `semifluidgeneratorfuels` map, served by
  the five Semifluid Generators the generator table covers; the builder
  asserts the legacy map stays empty. The Extreme Heat Exchanger routes as
  an ordinary recipe map, not a fuel. Fuel-map recipe types are
  per-voltage-tier (`rt~gregtech~<map>~<tier>`); extraction iterates by map
  prefix.
- **`machine_props`** — keyed by **machine item id**
  (`GREG_TECH_RECIPE_MAP_MACHINES.MACHINES_ITEM_ID`, deduplicated; display
  names are ambiguous — two distinct "Large Steam Turbine" items exist):
  per-item tier, multiblock flag, fuel efficiency, bonus records
  *(kind, base, per-component-tier scaling)* for parallels/speed/EU,
  auxiliary continuous draws (Volcanus pyrotheum), boiler steam yields,
  steam-turbine conversion, dynamo V/A/amperage class.
- **`turbine_rotors`** — per rotor item: base efficiency, per-fuel optimal
  flow / EU/t / efficiency (tight and loose), overflow tier, durability.
- **`renewable_seeds`** — the curated auto-infinite primitives (§4.6).
- **`meta.tier_voltages`** — EU/t per amp per tier, served through `/meta`
  so the web A×tier helper never hardcodes the GT voltage ladder.

### 5.3 Data sources: exporter extension is primary

Three candidate sources were evaluated per datum; the decision:

- **Primary: the `gregtech_machine_props` exporter plugin** in the
  project's own NESQL-exporter fork (shipped in 0.6.5): `GREG_TECH_GENERATOR`
  (efficiency/voltage/amps for every `MTEBasicGenerator`, single-block Steam
  Turbines included), `GREG_TECH_DYNAMO`, `GREG_TECH_TURBINE_ROTOR`
  (+`_FUEL_STATS`, per material × size from `TurbineStatCalculator`),
  `GREG_TECH_MULTIBLOCK_MACHINE` (+`_BONUSES`, matched against the **live
  `GT5U.MBTT.*` lang templates in-game** with `getMaxParallelRecipes()` as
  a cross-check — the builder never regex-parses raw localized strings
  offline; prior art documented that failure mode), and
  `GREG_TECH_LARGE_BOILER` (EU/t rating, both boiler generations). One
  verified quirk: `MTESteamTurbine` overloads `getEfficiency()` as
  steam-per-EU (3 EU per `6+tier` mB), so the exporter normalizes it to the
  true percentage `600 / getEfficiency()` against the 2 L/EU base rate.
  Cost: one full re-export (~1 h) plus `dump:convert`; plugin-only test
  exports keep iteration cheap.
- **Curated overlay** — only for the formula/prose multis the templates
  cannot express (EOH, PCB Factory, Compact Fusion, Spinmatron, Industrial
  Sledgehammer, Dangote Distillus, Electric Implosion Compressor), a small
  checked-in config audited against GT5U source, populated most-used-first
  like the structure pickers (§4.4); machines in neither source run
  bonus-less, flagged (§4.4).
- **Builder tooltip parsing** — demoted to an optional pre-re-export
  stopgap for the two rigid patterns only (`Fuel Efficiency: N%`, rotor stat
  tables), and to validation cross-checks (every extracted number asserted
  the way benzene 360 already is).

### 5.4 Fixture dump additions (phase 1)

`FixtureDump.cs` lacks even `RECIPE_SPECIAL_VALUE` today. Additions, each
mapping to a real-dump trap: the special-value column + a
`GREG_TECH_RECIPE_METADATA` cross-check row; a 1000 mB cell fuel AND a
144 mB one; a direct-fluid fuel; a boiler-style solid (coal-equivalent) and
an RTG-style row; a two-fluid-input IS_FUEL recipe (EHE reroute); an empty
fuel map; a map with recipe types across two tiers; two machine items at
different tiers/efficiencies on one map with tooltip lines; a §-coded
parallels line; a rotor item with a stat table; a >1A recipe from a 2A map;
renewability — a seed fluid, a derived item behind a higher-era machine, a
water→H₂+O₂→water cycle, and a deliberately unseeded world fluid
(lava-analog); a chanced expected-value-multiplying loop for the free-lunch
guard.

## 6. Architecture

### 6.1 Solver

- `FactorySolverService` and `ILinearProgramSolver` live in
  `Craftiger.Solver` — **pure, managed-only**. The solve is an orchestrator
  over one service per concern, each behind its interface: target
  normalization, the generator catalogue (fuels, turbine frontiers, hatch
  choice, band pruning), run variants (blocks, overclocks, steam), the
  cost-banded candidate walk, LP assembly, the auto-infinite fixpoint,
  diagnosis and plan interpretation; a `FactoryContext` bundles the
  artifact-derived inputs (graph, rate data, machines, seeds, steam rules,
  cost table, garage, weights). The HiGHS-backed implementation lives in
  the adapter project `src/Craftiger.Solver.Highs/` (model loader, layer
  runner, equilibration scaling, options), registered in DI by the API;
  Solver unit tests assert LP model construction against a recording
  fake, and end-to-end fixture solves live in a test project referencing
  the adapter. This keeps the CLAUDE.md "pure class library" rule true.
- **Backend (decided): [HiGHS](https://highs.dev) via `Highs.Native`** —
  verified: v1.15.1, first-party (published by the HiGHS team), MIT (HiGHS
  and package; one NOTICE line in the image), targets netstandard2.0
  (fine on .NET 10), bundles linux-x64/arm64 + win + mac natives in the
  nupkg — WSL2 and the container need no extra install. API is exactly the
  needed shape: `HighsModel` (column-compressed matrix, row/col bounds),
  `passLp`/`run`/`getSolution`, modification methods + automatic basis hot
  start for the sequential layers. Thread safety is undocumented: **one
  solver instance per solve, never shared**. Fallback (not chosen — far
  heavier for identical LP capability): Google.OrTools, Apache-2.0, behind
  the same interface. Phase-2 spike: confirm load on linux-x64/.NET 10 and
  whether the bundled HiGHS ≥ 1.9 (native lexicographic objectives, §4.3).
- **Execution model**: hard per-layer and per-solve time budgets (flow2
  needed 15 s/stage; expected sizes are far smaller) with timeout as a typed
  error; bounded concurrency (semaphore) so N factory solves cannot starve
  the cost path; the factory cache lock never blocks unrelated requests (the
  cost path's global-lock mistake is on record — do not repeat it). The
  "few thousand recipes" size claim was wrong — measured on the real
  artifact, the through-leaves closure of Polyethylene Bar is 32,118 items /
  164,695 producing recipes unfiltered and 28,641 / 133,687 at an HV garage;
  the tier filter barely bites because the blowup is low-tier (every
  macerator and crafting route of every reachable material). That unpruned
  LP (167,806 columns) broke the solver numerics — its matrix spans
  `2 × 10⁻⁴` chanced yields to `2.1 × 10⁹` sentinel amounts and no scaling
  rescued presolve — so **cost-guided pruning is enabled, not a fallback**:
  the candidate walk drops recipes no output of which prices within 4× the
  item's solved cost (+1 weight-unit floor for near-zero-priced items);
  pinned recipes always survive, and any pruning flags the plan
  (`routes_pruned`). Measured on PE Bar at an HV garage: 100,122 columns ×
  15,186 rows, matrix range `8 × 10⁻³ … 3 × 10²`, and the full four-layer
  lexicographic solve returns Optimal in ~9 s (resource 0.9 s, energy
  3.3 s, machines 8.4 s cumulative, canonicalization instant), 96 active
  columns. The traded-away routes are exotic byproduct synergies more than
  4× off the cost optimum. Generator (block, fuel) pairs are pruned the same
  way, by fuel weight per net EU/t (band 4×, floor 10⁻³, kept per quality
  band so a tier demand never starves). Energy-target solves at the
  worst-case everything-owned HV garage measure ~80 s — every surviving
  fuel's upstream chain joins the walk and the EU row couples the model, so
  presolve keeps ~170k live columns; real garages are far smaller and the
  factory cache absorbs first-solve latency, but this stays a watch item
  for the API budget.
- **Diagnostics** (three tiers, *never a bare "infeasible"*): (1) pre-LP
  checks reusing existing machinery — target outside the garage-legal
  closure → the existing `uncraftable`-style warning; energy target with no
  legal generator; consume target nothing can absorb; (2) pin conflicts
  ("the pin on X removes the only route"); (3) residual LP infeasibility
  diagnosed by an **elastic re-solve** (slack per balance row, minimize
  total slack — cheap with HiGHS) whose nonzero-slack items become per-item
  warning rows. Shipped mechanics: tier (2) is one feasibility probe with
  every pinned-away column freed — turning feasible means the pins are the
  cause, and each pin that removed a route warns `pin_conflict`; tier (3)
  puts shortfall slack on item and energy rows only (choice-slot link rows
  stay hard — they cannot conflict on their own), plus an excess slack on
  consume equalities, and the minimum-slack optimum names `infeasible_item`
  / `infeasible_energy` warnings. The bare `infeasible` warning survives
  only as the fallback when the elastic solve itself fails.

### 6.2 API

- `POST /api/factory/solve` — targets + garage + pins + weights context
  travel with the request, stateless as ever. Response: machine lines
  (recipe, **machine item id**, runs/s, machine count, OC steps, parallels,
  per-instance and line EU/t, flags), per-item flows and surplus, leaf
  inflows split auto-infinite vs priced, totals, warnings.
- **Cache**: `factoryId = hash(targets incl. kinds and normalized rates,
  garageHash, B, weightsHash, pins, artifact build id)` — weights and B
  price the objective, pins shape the feasible set, so all are in the key
  (the cost-solve key precedent: `(garageHash, B, weightsHash)`; the
  cost-side rule "pins never in the solve cache key" is re-scoped to cost
  solves in spec §5, and CLAUDE.md's invariant bullet is edited in the same
  commit). Valkey key `craftiger:{schema}:{pack}:{build_id}:factory:{factoryId}`,
  value = versioned compressed response DTO under the existing solve-store
  rules (magic, unparseable ⇒ recompute, write-before-answer, no expiry);
  single-flight per factoryId. Cross-replica determinism of the native
  solver is pinned by §4.3's settings + canonicalization layer and covered
  by an acceptance check; first writer wins.
- **Shipped mechanics**: the endpoint is `POST /api/factory/solve` per spec
  §8 (checks 60–63). The factoryId also hashes the priority order (layers
  run in it) and the mob-farms and bred-seeds toggles. The stored value is
  the plan alone — the `items` display lookup is rebuilt per response from
  the artifact, so cached values stay lean and display data always matches
  the serving build. Per-line EU/t ships as `FactoryLine.EuTPerMachine`
  (after-OC per-instance draw; negative = a generator's net emission) with
  the line total its product with `busyMachines`, and lines carry the
  after-OC duration. `timed_out` and `failed` plans answer but never cache
  — in memory or the store — so a retry recomputes; the per-solve budget is
  `ApiOptions:FactoryTimeLimitSeconds`. Shape errors are 400s; every solve
  outcome is a 200 with `status` + structured warnings. The cost solve
  behind a plan runs through the existing solve cache, so a factory hit
  never pays for one.

### 6.3 Garage delta

- **Power section**: fuel maps ship no `machine_eras` rows today, so
  generators are invisible to the garage. The builder emits garage rows per
  generator machine item (era derived from the item's own era solve, like
  every machine block); the garage UI gains a Power group listing them.
  Pseudo-recipe legality = the generator item's era ≤ default tier or
  explicitly owned.
- **Rotor picker** per large-turbine family (analogous to coils), stored
  client-side, defaulting to auto-best garage-legal, plus the per-line
  tight/loose fit toggle (§4.5).
- **Machine-block era gap** (found in phase 2): `machine_props.era` covers
  only the 256 signal blocks, so ordinary single blocks (the LV…UV tiers of
  every basic machine) have no craftability era. Until a schema bump ships
  eras for every `machine_items` row, the loader derives a single block's
  era from its own voltage tier — a close proxy — and skips multis without
  a props row (the anonymous fallback covers their maps, flagged).
- **Structure-part pickers** for non-coil bonus scaling axes, most-used
  families first (§4.4). Multi-controller granularity ("I own a Volcanus but not a Mega
  EBF") is era-derived in v1 — eligible blocks are those craftable at the
  garage's era — with per-plan machine exclusions deferred.

### 6.4 Web

- **Nav**: the app already has a routed nav (this doc's earlier "single
  view" claim was stale) — add one entry: `Crafting` (`#/`, label rename
  only; all localStorage keys are `gtnhp.*`, none label-derived), `Factory`
  (`#/factory`), `Price list` (`#/list`). Topbar status becomes per-route;
  the Weights button stays global (weights feed layer 1).
- **State**: a parallel store slice — targets persisted under
  `gtnhp.factory` (same versioned envelope as the cart), results/status in
  memory, staleness keyed by targets + garage + weights + **pins** (the
  crafting `settingsKey` deliberately omits pins and cannot be reused).
  Pins stay shared across tabs (decided) but become visible: a compact pins
  row in the Factory sidebar listing active pins inside the current closure,
  each with a clear button. One current target list in v1 (decided); named
  saves are deferred and, if added, arrive for both tabs. The mob-farm
  toggle (§4.6) lives beside the targets and persists with them.
- **Target editor**: rows mirror cart-row anatomy (Slot, name, controls,
  ghost ×). Entry = amount stepper + window (stepper + s/min/tick select);
  the row shows the normalized rate as secondary mono text ("= 0.53/s").
  Produce/consume is a two-state segmented control in the tag style with a
  persistent visual cue on consume rows. The energy row is a third kind:
  amps stepper × tier select → live EU/t (from `/meta` `tierVoltages`),
  EU/t directly editable; the tier select also sets the minimum generator
  output tier constraint (§4.2). The **global rate-unit picker**
  (tick/second/minute, default second) is display-only, lives in the
  results header, persists as `gtnhp.rateUnit`, and never travels in the
  request or the factoryId.
- **Flow graph**: reuse card geometry, Slot, viewport/pan/zoom/fit,
  orientation toggle, barycenter sweeps — behind an **adapter, not a fork**:
  cards keyed by recipe/machine line (not itemId — plans have multiple
  producers per item), edges fan out from every producer, layering by SCC
  condensation then longest path; no SEED cards (steady state has no first
  unit); LOOP tags may stay. RecipeCard gains a factory variant: all
  outputs undimmed with rate badges, no "not credited" line. **Rates live
  on slot badges, not edge labels** (edges stay clean bezier paths; the
  itemId hover-trace transfers unchanged); `fmtRate(value, unit, isFluid)`
  joins format.ts (unit-aware precision — `fmtCount` renders per-tick rates
  badly). Machine chrome reuses the three existing card anchors: header
  runs-badge → machine count ("6×", tooltip carries runs/s, busy machines,
  parallels, OC math); tag slot → at most two chips ("OC×2", "P×8");
  footer → after-OC per-machine "duration · EU/t".
- **Results column** (mirrors Crafting's reading order, no per-target
  chain-tabs — a plan is one network): warnings → totals strip (priced
  inflow cost, EU/t, machines) → Inputs grid (leaf inflows, auto-infinite
  marked **∞** — the decided label) → Byproducts & surplus grid ("+rate"
  badges, plus a "burnable surplus ≈ N EU/t" figure summing what the
  surplus streams would yield if burned — a fuels-table lookup, display
  only, never credited in the objective) → flow graph. Estimated lines (no bonus data, durationless,
  steam-unmodeled) carry an "EST" chip whose tooltip states the exact
  assumption, plus one accent-styled (not danger) note banner per plan;
  unaffected cards carry nothing — absence is the trust signal.
- **Infeasibility UX**: §6.1's three tiers surface as the existing warning
  rows naming items (clickable via openDetail), with the results-empty
  center panel pattern; toasts are for transport errors only.

- **Shipped mechanics**: the tab is live per spec §7 — nav rename, the
  `gtnhp.factory` slice (targets, toggles, priority; the plan stays in
  memory and a reload starts idle rather than auto-resolving), the target
  editor with the amps×tier energy row, the results column and the flow
  graph behind the shared `arrange()` layout core (`chainLayout.arrange`,
  used by both graphs; the factory adapter adds Tarjan SCC condensation +
  longest-path layering in `factoryLayout.ts`). The totals strip shows
  `exportEuT − drawEuT` as the net figure — the solver's `exportEuT` is the
  generators' gross emission, and the plan's own draw comes out of it
  first. The garage panel is shared; on the factory tab its relevance
  filter uses the new `deep` closure (`/api/machines?deep=true`, spec
  check 64) because the crafting closure stops at leaf-class items the
  factory solve expands through. Deviations, each with cause: the sidebar
  pins row lists **every** active pin, not just those inside the current
  closure — closure membership is not computable client-side and a hidden
  pin that keys the cache would shape plans silently; the burnable-surplus
  figure is deferred until the API ships it (fuel tables never reach the
  client, so it must be priced server-side); §6.3 stays open — the garage
  Power group needs builder-emitted generator era rows, and the rotor
  picker plus per-line tight/loose and variant pins need request plumbing
  through the solver stack.

## 7. The three examples, mapped

1. **32 PE/min at HV** — produce target `32/60 s⁻¹`; with resource-first
   priority the plan routes toward auto-infinite feedstocks where legal
   (water-chain oxygen, farm carbon) and prices the rest at leaf weights;
   the oxygen loop balances as flow; machine counts come from per-line
   `ceil(x · duration_eff / P)`.
2. **8A HV net from logs** — energy target `net(EU) ≥ 4096` with the
   exported amperage required from generators emitting at HV or above
   (§4.2); logs are auto-infinite seeds (TGS), pyrolyse benzene at
   360 EU/mB burns in the best garage-legal qualifying generators — turbine
   choice, OC level, and efficiency (85 % Turbo Gas Turbine vs Large Gas
   Turbine + rotor at optimal flow, dynamo-capped, Enet loss applied) are
   all solver decisions; the pyrolyse ovens' own draw is netted out.
   Pinning the pyrolyse recipe forces the sketch if a better route exists.
3. **64 ores / 32 s** — consume target `s ≤ 2/s` maximized first, then
   recovered value over leaf-class surplus (byproduct dusts at their
   weights), then the user's priority order; the LP chooses
   macerate/wash/thermal/sift splits per byproduct table and reports every
   output stream with its rate — "fully process" is the balance constraint,
   shortfalls name the blocking item.

## 8. Non-goals (v1)

- No logistics (buses, pipes, AE2, conveyor throughput) and no cable losses
  — machine lines are ideal and adjacent.
- No MILP / integer optimization; machine counts are ceil-of-continuous.
- Machine construction is free, instantaneous, and unbounded (per-machine
  count caps declined); no capital cost, no build-difficulty weighting.
- Maintenance assumed perfect; pollution ignored; rotor wear ignored; no
  startup transients (warm-up ramps, first-unit seeding) — steady state
  only.
- Off-optimal turbine flow (each line runs at its chosen fit's optimal
  flow) and GUI-configurable per-machine voltage knobs — pins plus the OC
  columns cover the need.
- Automated fishing and scrapbox loops as auto-infinite sources.
- Formula/prose multis without curated entries run bonus-less (flagged).

## 9. Decisions and remaining items

All design questions raised by earlier revisions are resolved; the rulings
are folded into their sections above. The decision log, for the spec merge:
HiGHS backend (§6.1); auto-infinite seeds = gtnh-flow catalog minus lava,
plus Air and Cobblestone, no fishing/scrapbox, EU free in the fixpoint,
catalyst-only recipes qualify, label ∞ (§4.6); mob farming an optional
per-factory toggle (§4.6); per-factory objective priority picker (§4.3);
energy targets are net export with generator output tier ≥ target tier
(§4.2); Enet output loss applied to generators, dynamos are capacity-only,
maintenance perfect (§4.5); steam is a second energy carrier with plannable
generation (§4.5); rotors auto-best with user override and a tight/loose
toggle, wear ignored (§4.5); RTG special value = burn years at the VOLTAGE
EU/t; plasma fuel = EU per 1000 L cell (§5.2); naquadah reactor
coolant/excited modes and combustion boosted modes are configurable
variants (§4.5); structure-part pickers and the curated overlay grow
most-used-first (§4.4, §5.3); generator tier identities confirmed as
dump-extracted (§5.1); TGS modeled with sapling + tool catalysts and tool
multipliers (§4.6); cleanroom is a running line, low-gravity gated by era
(§4.4); non-deprecated boiler items win (§4.5); the empty semifluid-boiler
map is an exporter bug to fix in phase 0 (§5.2); durationless lines free
and flagged (§4.4); burnable-surplus figure shown (§6.4); tabs, single
target list, shared visible pins, display-only unit picker defaulting to
per-second, no machine count caps (§6.4, §8).

To pin during implementation (facts to evidence, not decisions):

- Cleanroom controller EU/t draw — **pinned** from GT5-Unofficial
  5.09.54.20 `MTECleanroom`: 40 EU/t recipe, running draw `consumption/10`
  (4 EU/t steady on the LV hatch, which alone accepts 2 A), one energy
  hatch only; modeled at the garage tier's hatch (§4.4).
- The low-gravity era threshold — **pinned** as the configured T1-rocket
  era (`DimensionTierEras`, Moon: HV) shipped in `meta.environment`.
- TGS power-to-output multiplier formula — **pinned** from GT5-Unofficial
  5.09.54.20 `MTETreeFarm`: `2·tier² − 2·tier + 5`, draw `VP[tier]`, 100
  ticks (§4.6).
- Naquadah reactor coolant/excited mode constants — **pinned** from the
  pack's own `GoodGenerator.cfg` (rates, efficiencies, magnifications and
  the 2,400 L/s Liquid Air upkeep), shipped via `generator_modes` (§4.5).
- The v1 "most-used" lists: structure-part picker families and curated
  overlay multis (grown on demand).
- The plasma-cell volume discrepancy (user: 1000 L; one dump survey read
  144 mB) — settled by the builder assert on `FLUID_CONTAINER`.
- `Highs.Native` bundled HiGHS ≥ 1.9 (native lexicographic objectives) —
  **verified**: 1.15.1 loads on linux-x64/.NET 10, `addLinearObjective` with
  `blend_multi_objectives = false` honors priorities in both directions
  (negative weight maximizes a layer), and single-threaded fixed-seed
  simplex solves are deterministic across repeats.

## 10. Phasing and acceptance checks

0. **Exporter**: per-datum source decision table finalized; the
   `gregtech_machine_props` plugin (typed getters + MBTT template
   extraction + rotor stats + dynamo data + boiler steam yields +
   steam-turbine conversion + steam-machine draw); investigate and fix the
   empty `semifluidboilerfuels` export; plugin-only test exports, one full
   re-export + `dump:convert`, verification pass (row counts, reference
   asserts).
1. **Builder**: schema v9 (§5.2), the fixture additions (§5.4), extraction
   with per-map disposition asserts.
2. **Solver**: HiGHS spike (load + version), `ILinearProgramSolver` +
   adapter project, `FactorySolverService`, closure-size measurement,
   fixtures: loop balance, byproduct feedback, chanced rates, consume
   shortfall, energy net, OC-choice arithmetic, parallels division,
   free-lunch guard, elastic diagnosis.
3. **API**: `/api/factory/solve`, factoryId caching, single-flight, typed
   errors, `tierVoltages` in `/meta`.
4. **Web**: nav + rename, target editor, store slice, graph adapter,
   badges/chips/EST, infeasibility UX, rate-unit picker.

Acceptance checks feeding the spec §10 merge (one automated test each):
turbine line at optimal flow yields `optimalFlow × rotorEff` EU/t capped by
the dynamo, with the Enet output loss applied; generator fuel math includes
efficiency; an A×tier energy target rejects plans whose exporting
generators sit below the tier; a boiler → steam → steam-machine line
balances steam as a carrier; parallels divide machine counts and the
machine-efficiency objective; a TGS line applies the tool's output
multiplier; an RTG consumes pellets at `1/lifetime`; a leaf-overproducing
chain cannot push the resource layer below zero; the canonicalization layer
removes a zero-cost churn cycle and errors on a free-lunch cycle; the
auto-infinite fixpoint flips when the enabling machine leaves the garage,
and the mob-farm toggle adds/removes the mob-drop seeds; priority-order
permutations change the plan as specified; same inputs solved twice return
byte-identical plans; a pinned-away route reports the pin in the diagnosis;
the burnable-surplus figure matches the fuels table; each fuel-map unit
regime hits its reference assert.

## 11. Spec merge plan

Per phase, in the same commits as code: new spec sections for the model and
Factory UI; §2 glossary entries (Factory, target, machine line, surplus,
auto-infinite, factoryId, machine props); §3 artifact schema v9; §7 UI
deltas; §8 repo layout (`Craftiger.Solver.Highs`), endpoints, cache keys,
localStorage keys (`gtnhp.factory`, `gtnhp.rateUnit`); §9 — re-scope "pins
never part of the solve cache key" to cost solves, add the factory
counterpart rules ("no negative leaf credit", "surplus never discarded
silently", "no bare infeasible"); §10 acceptance checks above; CLAUDE.md —
layout and the pins invariant bullet, edited in the same commit that ships
the factory cache.
