import type { BomNode, BomResponse, BomStack } from './types'

export const SLOT = 40
export const SLOT_GAP = 4
export const PAD = 10
export const HEADER = 30
export const FOOTER = 22
export const ARROW = 26
const LAYER_GAP = 80
const CARD_GAP = 26
const LEAF_W = 200
const LEAF_H = 56
/** How far an edge running against the flow swings out before turning back. */
const LOOP_BEND = 80

export interface ChainCard {
  id: string
  kind: 'recipe' | 'leaf' | 'missing'
  node: BomNode | null
  amount: number
  inCols: number
  outCols: number
  x: number
  y: number
  w: number
  h: number
}

export interface ChainEdge {
  from: string
  to: string
  itemId: string
  /** Runs against the flow between two members of one loop. */
  loop: boolean
  x1: number
  y1: number
  x2: number
  y2: number
}

export interface ChainLayout {
  cards: ChainCard[]
  edges: ChainEdge[]
  width: number
  height: number
}

/** Which way the layers run: leaves left and target right, or leaves top and target bottom. */
export type ChainOrientation = 'horizontal' | 'vertical'

/** A seed node shares its item with a loop member, so its card needs its own key. */
export function nodeKey(node: BomNode): string {
  return node.seed ? `${node.itemId}#seed` : node.itemId
}

/** The crafting grid a shaped recipe draws its inputs on. */
export const GRID_COLUMNS = 3
export const GRID_ROWS = 3

/** Slots a shaped recipe's grid does not place — a fluid split from a bucket — render under it. */
export function gridExtras(node: BomNode): number[] {
  if (node.grid === null) {
    return []
  }
  const placed = new Set(node.grid)
  const count = node.inputsPerRun.length + node.catalysts.length
  const extras: number[] = []
  for (let slot = 0; slot < count; slot++) {
    if (!placed.has(slot)) {
      extras.push(slot)
    }
  }
  return extras
}

export function inputColumns(node: BomNode): number {
  const count = node.inputsPerRun.length + node.catalysts.length
  if (count === 0) {
    return 1
  }
  return node.grid !== null || node.machine === 'Crafting Table' ? GRID_COLUMNS : Math.min(4, count)
}

function inputRows(node: BomNode): number {
  if (node.grid !== null) {
    return GRID_ROWS + Math.ceil(gridExtras(node).length / GRID_COLUMNS)
  }
  return Math.max(1, Math.ceil((node.inputsPerRun.length + node.catalysts.length) / inputColumns(node)))
}

/** Where a slot sits in the card's input area: its first grid cell on a shaped recipe (or the
 * rows under the grid for a slot the grid does not place), its running position otherwise. */
export function slotPosition(node: BomNode, slot: number): { row: number; column: number } {
  const columns = inputColumns(node)
  if (node.grid !== null) {
    const cell = node.grid.indexOf(slot)
    if (cell !== -1) {
      return { row: Math.floor(cell / GRID_COLUMNS), column: cell % GRID_COLUMNS }
    }
    const extra = gridExtras(node).indexOf(slot)
    return { row: GRID_ROWS + Math.floor(extra / GRID_COLUMNS), column: extra % GRID_COLUMNS }
  }
  return { row: Math.floor(slot / columns), column: slot % columns }
}

export function outputColumns(node: BomNode): number {
  return Math.min(2, Math.max(1, node.outputs.length))
}

function gridWidth(columns: number): number {
  return columns * SLOT + (columns - 1) * SLOT_GAP
}

/** Narrow cards still fit their machine name in the header strip. */
const MIN_RECIPE_W = 210

function recipeSize(node: BomNode): { w: number; h: number } {
  const inCols = inputColumns(node)
  const outCols = outputColumns(node)
  const inRows = inputRows(node)
  const outRows = Math.max(1, Math.ceil(node.outputs.length / outCols))
  const rows = Math.max(inRows, outRows)
  return {
    w: Math.max(MIN_RECIPE_W, PAD * 2 + gridWidth(inCols) + ARROW + gridWidth(outCols)),
    h: HEADER + PAD * 2 + rows * SLOT + (rows - 1) * SLOT_GAP + FOOTER,
  }
}

/** Places recipe cards in topological layers from the leaves to the target, along the
/// chosen orientation; two barycenter sweeps keep edge crossings tolerable without a real solver. */
export function layoutChain(bom: BomResponse, orientation: ChainOrientation): ChainLayout {
  const recipeByItem = new Map(bom.nodes.filter((node) => !node.seed).map((node) => [node.itemId, node]))
  const consumed = new Set(bom.nodes.flatMap((node) => node.inputsPerRun.map((input) => input.itemId)))
  const leafAmounts = new Map(bom.leaves.map((leaf) => [leaf.itemId, leaf.wholeAmount]))
  const loopMembers = new Map<number, BomNode[]>()
  const loopSeeds = new Map<number, BomNode[]>()
  for (const node of bom.nodes) {
    if (node.loop !== null) {
      const group = node.seed ? loopSeeds : loopMembers
      group.set(node.loop, [...(group.get(node.loop) ?? []), node])
    }
  }

  const cards = new Map<string, ChainCard>()
  for (const [itemId, amount] of leafAmounts) {
    cards.set(itemId, {
      id: itemId,
      kind: 'leaf',
      node: null,
      amount,
      inCols: 0,
      outCols: 0,
      x: 0,
      y: 0,
      w: LEAF_W,
      h: LEAF_H,
    })
  }
  for (const itemId of consumed) {
    if (!cards.has(itemId) && !recipeByItem.has(itemId)) {
      cards.set(itemId, {
        id: itemId,
        kind: 'missing',
        node: null,
        amount: 0,
        inCols: 0,
        outCols: 0,
        x: 0,
        y: 0,
        w: LEAF_W,
        h: LEAF_H,
      })
    }
  }
  for (const node of bom.nodes) {
    const { w, h } = recipeSize(node)
    cards.set(nodeKey(node), {
      id: nodeKey(node),
      kind: 'recipe',
      node,
      amount: node.amount,
      inCols: inputColumns(node),
      outCols: outputColumns(node),
      x: 0,
      y: 0,
      w,
      h,
    })
  }

  // Every producer→consumer link by card id: each node's inputs feed it, and a loop's seed
  // feeds the loop members that consume its item.
  const links: { from: string; to: string; itemId: string; row: number; column: number }[] = []
  for (const node of bom.nodes) {
    const seen = new Set<string>()
    node.inputsPerRun.forEach((input, slotIndex) => {
      if (!seen.has(input.itemId) && cards.has(input.itemId)) {
        seen.add(input.itemId)
        links.push({ from: input.itemId, to: nodeKey(node), itemId: input.itemId, ...slotPosition(node, slotIndex) })
      }
    })
    if (node.seed && node.loop !== null) {
      for (const member of loopMembers.get(node.loop) ?? []) {
        const slotIndex = member.inputsPerRun.findIndex((input) => input.itemId === node.itemId)
        if (slotIndex !== -1) {
          links.push({ from: nodeKey(node), to: member.itemId, itemId: node.itemId, ...slotPosition(member, slotIndex) })
        }
      }
    }
  }

  // Nodes arrive targets-first, so the reversed order sees producers before consumers. A
  // loop's members share one layer, past every input outside the loop and past its seed.
  const layerOf = new Map<string, number>()
  for (const card of cards.values()) {
    if (card.kind !== 'recipe') {
      layerOf.set(card.id, 0)
    }
  }
  const inputLayer = (inputs: BomStack[], except: ReadonlySet<string>) =>
    Math.max(0, ...inputs.filter((input) => !except.has(input.itemId)).map((input) => layerOf.get(input.itemId) ?? 0))
  for (const node of bom.nodes.slice().reverse()) {
    const key = nodeKey(node)
    if (layerOf.has(key)) {
      continue
    }
    if (node.loop === null || node.seed) {
      layerOf.set(key, inputLayer(node.inputsPerRun, new Set()) + 1)
      continue
    }
    const members = loopMembers.get(node.loop) ?? [node]
    const memberIds = new Set(members.map((member) => member.itemId))
    let layer = Math.max(...members.map((member) => inputLayer(member.inputsPerRun, memberIds) + 1))
    for (const seed of loopSeeds.get(node.loop) ?? []) {
      layer = Math.max(layer, (layerOf.get(nodeKey(seed)) ?? 0) + 1)
    }
    for (const member of members) {
      layerOf.set(member.itemId, layer)
    }
  }

  const layerCount = Math.max(0, ...layerOf.values()) + 1
  const layers: string[][] = Array.from({ length: layerCount }, () => [])
  for (const itemId of leafAmounts.keys()) {
    layers[layerOf.get(itemId) ?? 0].push(itemId)
  }
  for (const card of cards.values()) {
    if (card.kind === 'missing') {
      layers[0].push(card.id)
    }
  }
  for (const node of bom.nodes.slice().reverse()) {
    layers[layerOf.get(nodeKey(node)) ?? 0].push(nodeKey(node))
  }

  const producersOf = new Map<string, string[]>()
  const consumersOf = new Map<string, string[]>()
  for (const link of links) {
    producersOf.set(link.to, [...(producersOf.get(link.to) ?? []), link.from])
    consumersOf.set(link.from, [...(consumersOf.get(link.from) ?? []), link.to])
  }

  const position = new Map<string, number>()
  const reindex = () => {
    for (const layer of layers) {
      layer.forEach((id, index) => position.set(id, index))
    }
  }
  const barycenter = (neighbors: string[], fallback: number): number =>
    neighbors.length === 0
      ? fallback
      : neighbors.reduce((sum, id) => sum + (position.get(id) ?? 0), 0) / neighbors.length
  reindex()
  for (let layer = 1; layer < layers.length; layer++) {
    layers[layer] = layers[layer]
      .map((id, index) => ({ id, key: barycenter(producersOf.get(id) ?? [], index) }))
      .toSorted((a, b) => a.key - b.key)
      .map((entry) => entry.id)
    reindex()
  }
  for (let layer = layers.length - 2; layer >= 0; layer--) {
    layers[layer] = layers[layer]
      .map((id, index) => ({ id, key: barycenter(consumersOf.get(id) ?? [], index) }))
      .toSorted((a, b) => a.key - b.key)
      .map((entry) => entry.id)
    reindex()
  }

  const { width, height } =
    orientation === 'vertical' ? placeRows(layers, cards) : placeColumns(layers, cards)

  const edges: ChainEdge[] = links.map((link) => {
    const from = cards.get(link.from)!
    const to = cards.get(link.to)!
    const loop =
      from.node !== null && to.node !== null && !from.node.seed && !to.node.seed &&
      from.node.loop !== null && from.node.loop === to.node.loop
    return orientation === 'vertical'
      ? verticalEdge(from, to, link.itemId, link.column, loop)
      : horizontalEdge(from, to, link.itemId, link.row, loop)
  })

  // Loop arcs swing out past the last layer; the extent must include them or fit clips them.
  const hasLoop = edges.some((edge) => edge.loop)
  return {
    cards: [...cards.values()],
    edges,
    width: width + (hasLoop && orientation === 'horizontal' ? LOOP_BEND : 0),
    height: height + (hasLoop && orientation === 'vertical' ? LOOP_BEND : 0),
  }
}

/** One column per layer, left to right; each column is centered on the tallest one. */
function placeColumns(layers: string[][], cards: Map<string, ChainCard>): { width: number; height: number } {
  const columnWidths = layers.map((layer) =>
    Math.max(0, ...layer.map((id) => cards.get(id)!.w)),
  )
  const columnHeights = layers.map((layer) =>
    layer.reduce((sum, id) => sum + cards.get(id)!.h, 0) + Math.max(0, layer.length - 1) * CARD_GAP,
  )
  const height = Math.max(0, ...columnHeights)

  let x = 0
  layers.forEach((layer, index) => {
    let y = (height - columnHeights[index]) / 2
    for (const id of layer) {
      const card = cards.get(id)!
      card.x = x
      card.y = y
      y += card.h + CARD_GAP
    }
    x += columnWidths[index] + LAYER_GAP
  })
  return { width: Math.max(0, x - LAYER_GAP), height }
}

/** One row per layer, top to bottom; each row is centered on the widest one. */
function placeRows(layers: string[][], cards: Map<string, ChainCard>): { width: number; height: number } {
  const rowHeights = layers.map((layer) =>
    Math.max(0, ...layer.map((id) => cards.get(id)!.h)),
  )
  const rowWidths = layers.map((layer) =>
    layer.reduce((sum, id) => sum + cards.get(id)!.w, 0) + Math.max(0, layer.length - 1) * CARD_GAP,
  )
  const width = Math.max(0, ...rowWidths)

  let y = 0
  layers.forEach((layer, index) => {
    let x = (width - rowWidths[index]) / 2
    for (const id of layer) {
      const card = cards.get(id)!
      card.x = x
      card.y = y
      x += card.w + CARD_GAP
    }
    y += rowHeights[index] + LAYER_GAP
  })
  return { width, height: Math.max(0, y - LAYER_GAP) }
}

/** Leaves the producer's right edge at its body's middle and enters the consuming slot's row. */
function horizontalEdge(
  from: ChainCard, to: ChainCard, itemId: string, row: number, loop: boolean,
): ChainEdge {
  return {
    from: from.id,
    to: to.id,
    itemId,
    loop,
    x1: from.x + from.w,
    y1: from.y + (from.kind === 'recipe' ? HEADER + (from.h - HEADER - FOOTER) / 2 : from.h / 2),
    x2: to.x,
    y2: to.y + HEADER + PAD + row * (SLOT + SLOT_GAP) + SLOT / 2,
  }
}

/** Leaves the producer's bottom edge under its output grid and enters the consuming slot's column. */
function verticalEdge(
  from: ChainCard, to: ChainCard, itemId: string, column: number, loop: boolean,
): ChainEdge {
  return {
    from: from.id,
    to: to.id,
    itemId,
    loop,
    x1: from.x + (from.kind === 'recipe' ? from.w - PAD - gridWidth(from.outCols) / 2 : from.w / 2),
    y1: from.y + from.h,
    x2: to.x + PAD + column * (SLOT + SLOT_GAP) + SLOT / 2,
    y2: to.y,
  }
}

/** A cubic curve that leaves and arrives along the flow axis; an edge running against the
 * flow (one loop member feeding another) swings out and back instead of folding flat. */
export function edgePath(edge: ChainEdge, orientation: ChainOrientation): string {
  if (orientation === 'vertical') {
    const bend = edge.y2 > edge.y1 ? Math.min(60, (edge.y2 - edge.y1) / 2) : LOOP_BEND
    return `M ${edge.x1} ${edge.y1} C ${edge.x1} ${edge.y1 + bend}, ${edge.x2} ${edge.y2 - bend}, ${edge.x2} ${edge.y2}`
  }
  const bend = edge.x2 > edge.x1 ? Math.min(60, (edge.x2 - edge.x1) / 2) : LOOP_BEND
  return `M ${edge.x1} ${edge.y1} C ${edge.x1 + bend} ${edge.y1}, ${edge.x2 - bend} ${edge.y2}, ${edge.x2} ${edge.y2}`
}