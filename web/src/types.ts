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