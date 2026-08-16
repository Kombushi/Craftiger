import type { BomNode, BomResponse } from './types'

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

export function inputColumns(node: BomNode): number {
  const count = node.inputsPerRun.length
  if (count === 0) {
    return 1
  }
  return node.machine === 'Crafting Table' ? 3 : Math.min(4, count)
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
  const inRows = Math.max(1, Math.ceil(node.inputsPerRun.length / inCols))
  const outRows = Math.max(1, Math.ceil(node.outputs.length / outCols))
  const rows = Math.max(inRows, outRows)
  return {
    w: Math.max(MIN_RECIPE_W, PAD * 2 + gridWidth(inCols) + ARROW + gridWidth(outCols)),
    h: HEADER + PAD * 2 + rows * SLOT + (rows - 1) * SLOT_GAP + FOOTER,
  }
}

/** Places recipe cards in topological layers, leaves on the left, the target rightmost;
/// two barycenter sweeps keep edge crossings tolerable without a real solver. */
export function layoutChain(bom: BomResponse): ChainLayout {
  const recipeByItem = new Map(bom.nodes.map((node) => [node.itemId, node]))
  const consumed = new Set(bom.nodes.flatMap((node) => node.inputsPerRun.map((input) => input.itemId)))
  const leafAmounts = new Map(bom.leaves.map((leaf) => [leaf.itemId, leaf.amount]))

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
    cards.set(node.itemId, {
      id: node.itemId,
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

  // Nodes arrive targets-first, so the reversed order sees producers before consumers.
  const layerOf = new Map<string, number>()
  for (const card of cards.values()) {
    if (card.kind !== 'recipe') {
      layerOf.set(card.id, 0)
    }
  }
  for (const node of bom.nodes.slice().reverse()) {
    const deepest = Math.max(
      0,
      ...node.inputsPerRun.map((input) => layerOf.get(input.itemId) ?? 0),
    )
    layerOf.set(node.itemId, deepest + 1)
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
    layers[layerOf.get(node.itemId) ?? 0].push(node.itemId)
  }

  const producersOf = (itemId: string): string[] => {
    const node = recipeByItem.get(itemId)
    if (!node) {
      return []
    }
    return [...new Set(node.inputsPerRun.map((input) => input.itemId))]
  }
  const consumersOf = new Map<string, string[]>()
  for (const node of bom.nodes) {
    for (const input of new Set(node.inputsPerRun.map((i) => i.itemId))) {
      const list = consumersOf.get(input) ?? []
      list.push(node.itemId)
      consumersOf.set(input, list)
    }
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
      .map((id, index) => ({ id, key: barycenter(producersOf(id), index) }))
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
  const width = Math.max(0, x - LAYER_GAP)

  const edges: ChainEdge[] = []
  for (const node of bom.nodes) {
    const to = cards.get(node.itemId)!
    const seen = new Set<string>()
    node.inputsPerRun.forEach((input, slotIndex) => {
      if (seen.has(input.itemId)) {
        return
      }
      seen.add(input.itemId)
      const from = cards.get(input.itemId)
      if (!from) {
        return
      }
      const row = Math.floor(slotIndex / to.inCols)
      edges.push({
        from: from.id,
        to: to.id,
        itemId: input.itemId,
        x1: from.x + from.w,
        y1: from.y + (from.kind === 'recipe' ? HEADER + (from.h - HEADER - FOOTER) / 2 : from.h / 2),
        x2: to.x,
        y2: to.y + HEADER + PAD + row * (SLOT + SLOT_GAP) + SLOT / 2,
      })
    })
  }

  return { cards: [...cards.values()], edges, width, height }
}