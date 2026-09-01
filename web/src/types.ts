export interface CoilDto {
  name: string
  maxHeat: number
  tier: number
}

export interface MachineDto {
  name: string
  hasMultiblockSwitch: boolean
  heatGated: boolean
  alwaysOwned: boolean
  era: number | null
  multiblockOnly: boolean
}

export interface AtlasDto {
  width: number
  height: number
  cell: number
}

export interface MetaResponse {
  packVersion: string
  tierNames: string[]
  /** EU/t per amp per tier, indexed like tierNames. */
  tierVoltages: number[]
  coils: CoilDto[]
  machines: MachineDto[]
  atlas: AtlasDto | null
}

export interface SolveResponse {
  solveId: string
  pricedItems: number
  converged: boolean
}

export interface ItemSummary {
  itemId: string
  name: string
  atlasIdx: number
  cost: number | null
  uncraftable: boolean
}

export interface ListResponse {
  items: ItemSummary[]
  total: number
  page: number
  pageSize: number
}

export interface ItemRef {
  name: string
  atlasIdx: number
  isFluid: boolean
  leafClass: string | null
  cost: number | null
  uncraftable: boolean
  /** The pack's stack size; null for fluids. */
  maxStack: number | null
  aliases?: string[] | null
}

export interface BomStack {
  itemId: string
  amount: number
}

export interface BomLeaf {
  itemId: string
  amount: number
  wholeAmount: number
}

export interface OutputRow {
  itemId: string
  amount: number
  chance: number
}

export interface BomNode {
  itemId: string
  amount: number
  runs: number
  wholeAmount: number
  wholeRuns: number
  recipeId: string
  machine: string
  tier: number
  multiTier: number | null
  heat: number | null
  durationTicks: number
  euT: number
  inputsPerRun: BomStack[]
  catalysts: BomStack[]
  outputs: OutputRow[]
  loop: number | null
  seed: boolean
  /** A shaped crafting recipe's nine cells, row-major: the slot each holds — inputsPerRun
   * first, then catalysts — or null for an empty cell; null when the recipe has no shape. */
  grid: (number | null)[] | null
}

export interface BomWarning {
  kind: string
  itemId: string
}

export interface BomTargetResult {
  itemId: string
  count: number
  recipeId: string | null
  inputs: BomStack[]
}

export interface BomResponse {
  targets: BomTargetResult[]
  leaves: BomLeaf[]
  warnings: BomWarning[]
  nodes: BomNode[]
  items: Record<string, ItemRef>
}

export interface SlotAlternative {
  itemId: string
  amount: number
  cost: number | null
}

export interface RecipeDto {
  recipeId: string
  machine: string
  tier: number
  multiTier: number | null
  heat: number | null
  durationTicks: number
  euT: number
  candidateCost: number | null
  slots: SlotAlternative[][]
  chosen: string[]
  catalysts: SlotAlternative[][]
  outputs: OutputRow[]
  /** A shaped crafting recipe's nine cells, row-major: the slot each holds — slots first,
   * then catalysts — or null for an empty cell; null when the recipe has no shape. */
  grid: (number | null)[] | null
  /** A factory-only row's scope ('factory', 'factory_mob', 'factory_bred'); null is a crafting recipe. */
  scope?: string | null
  /** The cheapest block that runs the row, for its icon. */
  machineItemId?: string | null
}

export interface ItemDetail {
  itemId: string
  name: string
  atlasIdx: number
  leafClass: string | null
  cost: number | null
  uncraftable: boolean
  bestRecipeId: string | null
  recipes: RecipeDto[]
  items: Record<string, ItemRef>
}

export interface GarageState {
  defaultTier: number
  machines: Record<string, number | null>
  builtMultiblocks: string[]
  coils: Record<string, string>
}

export interface CartEntry {
  itemId: string
  count: number
  name: string
  atlasIdx: number
  isFluid: boolean
}

export type RateUnit = 'tick' | 'second' | 'minute'

/** A produce or consume target as the editor stores it: amount per window, normalized to per-second for the request. */
export interface FactoryItemTarget {
  kind: 'produce' | 'consume'
  itemId: string
  name: string
  atlasIdx: number
  amount: number
  window: number
  windowUnit: RateUnit
}

/** The energy target: amps × tier gives the EU/t unless it was edited directly; the tier also floors the generators' output tier. */
export interface FactoryEnergyTarget {
  kind: 'energy'
  amps: number
  tier: number
  euT: number
}

export type FactoryTargetState = FactoryItemTarget | FactoryEnergyTarget

export interface FactoryLineFlow {
  itemId: string
  perSecond: number
}

export interface FactoryLine {
  recipeId: string
  machine: string
  machineItemId: string | null
  runsPerSecond: number
  ocSteps: number
  parallels: number
  busyMachines: number
  durationless: boolean
  estimated: boolean
  /** One run after overclocking, in seconds; 0 on durationless and generator lines. */
  durationSeconds: number
  /** One busy instance's draw in EU/t; negative is a generator's net emission. */
  euTPerMachine: number
  lineEuT: number
  inputs: FactoryLineFlow[] | null
  outputs: FactoryLineFlow[] | null
}

export interface FactoryItemFlow {
  itemId: string
  produced: number
  consumed: number
  surplus: number
  supplied: number
  autoInfinite: boolean
}

export interface FactoryInflow {
  itemId: string
  rate: number
  weight: number
  autoInfinite: boolean
}

export interface FactoryWarning {
  kind: string
  itemId: string
}

export type FactoryStatus = 'solved' | 'infeasible' | 'unbounded' | 'timed_out' | 'failed'

/** One Planner step as a picker delivers it: the id a pipeline names, a display snapshot, the optional block/OC lock, and the recipe's scope. */
export interface PlannerStep {
  id: string
  label: string
  atlasIdx: number
  machine: string
  machineItemId: string | null
  ocSteps: number | null
  /** The producer catalog's scope; farm consent derives from it, null is a crafting recipe or a generator line. */
  scope: string | null
}

/** A recipe or generator line placed on the Planner grid. */
export interface StepNode extends PlannerStep {
  kind: 'step'
  x: number
  y: number
}

/** A free source the user declares on hand; a rate turns it into a fixed intake the pipeline must absorb. */
export interface InputNode {
  kind: 'input'
  itemId: string
  name: string
  atlasIdx: number
  /** null = unbounded free supply. */
  amount: number | null
  window: number
  windowUnit: RateUnit
  x: number
  y: number
}

/** A produce target on the grid. */
export interface OutputNode {
  kind: 'output'
  itemId: string
  name: string
  atlasIdx: number
  amount: number
  window: number
  windowUnit: RateUnit
  x: number
  y: number
}

/** The energy export target on the grid; generator steps feed it. */
export interface EnergyNode {
  kind: 'energy'
  amps: number
  tier: number
  euT: number
  x: number
  y: number
}

export type PlannerNode = StepNode | InputNode | OutputNode | EnergyNode

export interface GeneratorLineDto {
  id: string
  map: string
  machineItemId: string
  fuelItemId: string
  tier: number
  netEuT: number
  fuelPerSecond: number
  variant: string | null
}

export interface GeneratorCatalogResponse {
  lines: GeneratorLineDto[]
  items: Record<string, ItemRef>
}

export interface FactoryResponse {
  factoryId: string
  status: FactoryStatus
  lines: FactoryLine[]
  flows: FactoryItemFlow[]
  inflows: FactoryInflow[]
  warnings: FactoryWarning[]
  pricedInflowCost: number
  drawEuT: number
  exportEuT: number
  busyMachines: number
  items: Record<string, ItemRef>
}