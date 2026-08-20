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
  catalysts: SlotAlternative[][]
  outputs: OutputRow[]
}

export interface ItemDetail {
  itemId: string
  name: string
  atlasIdx: number
  leafClass: string | null
  cost: number | null
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