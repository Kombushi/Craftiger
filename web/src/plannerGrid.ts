import {
  FOOTER,
  HEADER,
  LEAF_H,
  LEAF_W,
  PAD,
  SLOT,
  SLOT_GAP,
  arrange,
  bodySize,
  type ChainEdge,
  type LayoutLink,
} from './chainLayout'
import { condense } from './factoryLayout'
import type { FactoryLine, FactoryLineFlow, FactoryResponse, PlannerNode } from './types'

export const IO_W = 250
export const IO_H = 96
export const ENERGY_W = 270
export const GRID_SNAP = 20
/** The strip of per-step controls (lock, overclock, remove) under a step card's body. */
export const STEP_CONTROLS = 28
const GHOST_GAP = 90
const GHOST_STACK = 14

/** A step's plan lines folded into one card: dominant variant identity, summed machines and flows. */
export interface MergedLine {
  machine: string
  machineItemId: string | null
  ocSteps: number
  parallels: number
  busyMachines: number
  runsPerSecond: number
  durationSeconds: number
  euTPerMachine: number
  durationless: boolean
  estimated: boolean
  inputs: FactoryLineFlow[]
  outputs: FactoryLineFlow[]
}

/** One card on the grid: a placed node, or a ghost the solve conjured around them. */
export interface GridCard {
  id: string
  kind: 'line' | 'input' | 'output' | 'energy' | 'ghost-in' | 'ghost-out'
  node: PlannerNode | null
  itemId: string | null
  line: MergedLine | null
  inCols: number
  outCols: number
  x: number
  y: number
  w: number
  h: number
}

export interface GridModel {
  cards: GridCard[]
  edges: ChainEdge[]
  width: number
  height: number
}

/** The stable identity a node's card and its persisted position share. */
export function nodeId(node: PlannerNode): string {
  switch (node.kind) {
    case 'step':
      return node.id
    case 'input':
      return `in:${node.itemId}`
    case 'output':
      return `out:${node.itemId}`
    case 'energy':
      return 'energy'
  }
}

export const snap = (value: number): number => Math.max(0, Math.round(value / GRID_SNAP) * GRID_SNAP)

function mergeLines(lines: FactoryLine[]): MergedLine | null {
  if (lines.length === 0) {
    return null
  }
  const dominant = lines.reduce((best, line) => (line.busyMachines >= best.busyMachines ? line : best))
  const fold = (flows: (FactoryLineFlow[] | null)[]) => {
    const byItem = new Map<string, number>()
    for (const list of flows) {
      for (const flow of list ?? []) {
        byItem.set(flow.itemId, (byItem.get(flow.itemId) ?? 0) + flow.perSecond)
      }
    }
    return [...byItem].map(([itemId, perSecond]) => ({ itemId, perSecond }))
  }
  return {
    machine: dominant.machine,
    machineItemId: dominant.machineItemId,
    ocSteps: dominant.ocSteps,
    parallels: dominant.parallels,
    busyMachines: lines.reduce((sum, line) => sum + line.busyMachines, 0),
    runsPerSecond: lines.reduce((sum, line) => sum + line.runsPerSecond, 0),
    durationSeconds: dominant.durationSeconds,
    euTPerMachine: dominant.euTPerMachine,
    durationless: dominant.durationless,
    estimated: dominant.estimated,
    inputs: fold(lines.map((line) => line.inputs)),
    outputs: fold(lines.map((line) => line.outputs)),
  }
}

function lineSize(line: MergedLine | null): { inCols: number; outCols: number; w: number; h: number } {
  const inputs = line?.inputs.length ?? 0
  const outputs = line?.outputs.length ?? 0
  const inCols = Math.min(4, Math.max(1, inputs))
  const outCols = Math.min(2, Math.max(1, outputs))
  const { w, h } = bodySize(
    inCols,
    Math.max(1, Math.ceil(inputs / inCols)),
    outCols,
    Math.max(1, Math.ceil(outputs / outCols)),
  )
  return { inCols, outCols, w, h: h + STEP_CONTROLS }
}

/** Like the chain's horizontal edge, but grid consumers may be plain cards anchored at mid-height. */
function gridEdge(from: GridCard, to: GridCard, itemId: string, row: number): ChainEdge {
  return {
    from: from.id,
    to: to.id,
    itemId,
    loop: false,
    x1: from.x + from.w,
    y1: from.y + (from.kind === 'line' ? HEADER + (from.h - HEADER - FOOTER - STEP_CONTROLS) / 2 : from.h / 2),
    x2: to.x,
    y2: to.y + (to.kind === 'line' ? HEADER + PAD + row * (SLOT + SLOT_GAP) + SLOT / 2 : to.h / 2),
  }
}

interface Consumer {
  card: string
  slot: number
  inCols: number
}

/** Cards for the nodes plus flow bookkeeping; positions are filled by the caller. */
function buildCards(nodes: PlannerNode[], plan: FactoryResponse | null) {
  const cards = new Map<string, GridCard>()
  const producers = new Map<string, string[]>()
  const consumers = new Map<string, Consumer[]>()
  const generatorCards: string[] = []
  const linesOf = new Map<string, FactoryLine[]>()
  for (const line of plan?.lines ?? []) {
    linesOf.set(line.recipeId, [...(linesOf.get(line.recipeId) ?? []), line])
  }

  for (const node of nodes) {
    const id = nodeId(node)
    if (node.kind === 'step') {
      const line = mergeLines(linesOf.get(node.id) ?? [])
      const { inCols, outCols, w, h } = lineSize(line)
      cards.set(id, { id, kind: 'line', node, itemId: null, line, inCols, outCols, x: node.x, y: node.y, w, h })
      line?.inputs.forEach((flow, slot) => {
        consumers.set(flow.itemId, [...(consumers.get(flow.itemId) ?? []), { card: id, slot, inCols }])
      })
      for (const flow of line?.outputs ?? []) {
        producers.set(flow.itemId, [...(producers.get(flow.itemId) ?? []), id])
      }
      if (line !== null && line.euTPerMachine < 0) {
        generatorCards.push(id)
      }
    } else if (node.kind === 'energy') {
      cards.set(id, {
        id, kind: 'energy', node, itemId: null, line: null, inCols: 0, outCols: 0,
        x: node.x, y: node.y, w: ENERGY_W, h: IO_H,
      })
    } else {
      const kind = node.kind
      cards.set(id, {
        id, kind, node, itemId: node.itemId, line: null, inCols: 0, outCols: 0,
        x: node.x, y: node.y, w: IO_W, h: IO_H,
      })
      if (kind === 'input') {
        producers.set(node.itemId, [...(producers.get(node.itemId) ?? []), id])
      } else {
        consumers.set(node.itemId, [...(consumers.get(node.itemId) ?? []), { card: id, slot: 0, inCols: 1 }])
      }
    }
  }

  // Ghosts: what the solve supplies from outside without an input node, and what it
  // leaves over without an output node — the click-to-adopt frontier of the grid.
  // Surplus ghosts come first, off real producers only: a purchase never leaves anything over.
  const inputItems = new Set(nodes.flatMap((node) => (node.kind === 'input' ? [node.itemId] : [])))
  const outputItems = new Set(nodes.flatMap((node) => (node.kind === 'output' ? [node.itemId] : [])))
  for (const flow of plan?.flows ?? []) {
    // The layer corridor leaves low-percent slivers, and an unbounded input's overbuy is
    // not a byproduct; only real surplus off a real producer earns a ghost.
    if (
      flow.surplus > Math.max(1e-6, flow.produced * 0.02) &&
      !outputItems.has(flow.itemId) &&
      !inputItems.has(flow.itemId) &&
      producers.has(flow.itemId)
    ) {
      const id = `ghost-out:${flow.itemId}`
      cards.set(id, {
        id, kind: 'ghost-out', node: null, itemId: flow.itemId, line: null,
        inCols: 0, outCols: 0, x: 0, y: 0, w: LEAF_W, h: LEAF_H,
      })
      consumers.set(flow.itemId, [...(consumers.get(flow.itemId) ?? []), { card: id, slot: 0, inCols: 1 }])
    }
  }
  for (const inflow of plan?.inflows ?? []) {
    if (!inputItems.has(inflow.itemId) && consumers.has(inflow.itemId)) {
      const id = `ghost-in:${inflow.itemId}`
      cards.set(id, {
        id, kind: 'ghost-in', node: null, itemId: inflow.itemId, line: null,
        inCols: 0, outCols: 0, x: 0, y: 0, w: LEAF_W, h: LEAF_H,
      })
      producers.set(inflow.itemId, [...(producers.get(inflow.itemId) ?? []), id])
    }
  }

  const links: LayoutLink[] = []
  for (const [itemId, wanting] of consumers) {
    for (const from of producers.get(itemId) ?? []) {
      for (const want of wanting) {
        if (from !== want.card) {
          links.push({
            from,
            to: want.card,
            itemId,
            row: Math.floor(want.slot / want.inCols),
            column: want.slot % want.inCols,
            loop: false,
          })
        }
      }
    }
  }
  if (cards.has('energy')) {
    for (const from of generatorCards) {
      links.push({ from, to: 'energy', itemId: '⚡', row: 0, column: 0, loop: false })
    }
  }
  return { cards, links }
}

/** The grid at the user's positions: ghosts hug the cards they feed or drain, and the whole
 * model is shifted to a zero origin so the viewport's fit works unchanged. */
export function buildGrid(nodes: PlannerNode[], plan: FactoryResponse | null): GridModel {
  const { cards, links } = buildCards(nodes, plan)

  const ghostRank = new Map<string, number>()
  for (const card of cards.values()) {
    if (card.kind === 'ghost-in') {
      const wanting = links.filter((link) => link.from === card.id)
      const first = wanting
        .map((link) => cards.get(link.to)!)
        .reduce((best, c) => (c.x < best.x ? c : best), cards.get(wanting[0].to)!)
      const rank = ghostRank.get(first.id) ?? 0
      ghostRank.set(first.id, rank + 1)
      card.x = first.x - LEAF_W - GHOST_GAP
      card.y = first.y + rank * (LEAF_H + GHOST_STACK)
    } else if (card.kind === 'ghost-out') {
      const feeding = links.filter((link) => link.to === card.id)
      const first = feeding
        .map((link) => cards.get(link.from)!)
        .reduce((best, c) => (c.x + c.w > best.x + best.w ? c : best), cards.get(feeding[0].from)!)
      const rank = ghostRank.get(`out:${first.id}`) ?? 0
      ghostRank.set(`out:${first.id}`, rank + 1)
      card.x = first.x + first.w + GHOST_GAP
      card.y = first.y + rank * (LEAF_H + GHOST_STACK)
    }
  }

  let minX = 0
  let minY = 0
  for (const card of cards.values()) {
    minX = Math.min(minX, card.x)
    minY = Math.min(minY, card.y)
  }
  let width = 0
  let height = 0
  for (const card of cards.values()) {
    card.x -= minX
    card.y -= minY
    width = Math.max(width, card.x + card.w)
    height = Math.max(height, card.y + card.h)
  }

  const edges = links.map((link) =>
    gridEdge(cards.get(link.from)!, cards.get(link.to)!, link.itemId, link.row))
  return { cards: [...cards.values()], edges, width, height }
}

/** Layered auto-positions for every placed node — the Tidy button — via the same SCC
 * condensation and longest-path layering the Factory graph uses. */
export function tidyPositions(nodes: PlannerNode[], plan: FactoryResponse | null): Map<string, { x: number; y: number }> {
  const { cards, links } = buildCards(nodes, plan)
  const componentOf = condense(cards.keys(), links)
  const componentPreds = new Map<number, Set<number>>()
  for (const link of links) {
    const from = componentOf.get(link.from)!
    const to = componentOf.get(link.to)!
    if (from !== to) {
      componentPreds.set(to, (componentPreds.get(to) ?? new Set()).add(from))
    }
  }
  const componentLayer = new Map<number, number>()
  const layerOfComponent = (component: number): number => {
    const known = componentLayer.get(component)
    if (known !== undefined) {
      return known
    }
    componentLayer.set(component, 0)
    const layer = Math.max(
      0,
      ...[...(componentPreds.get(component) ?? [])].map((pred) => layerOfComponent(pred) + 1),
    )
    componentLayer.set(component, layer)
    return layer
  }
  const layerOf = new Map<string, number>()
  for (const card of cards.values()) {
    layerOf.set(card.id, layerOfComponent(componentOf.get(card.id)!))
  }
  const layerCount = Math.max(0, ...layerOf.values()) + 1
  const layers: string[][] = Array.from({ length: layerCount }, () => [])
  for (const card of cards.values()) {
    layers[layerOf.get(card.id)!].push(card.id)
  }
  arrange(layers, cards, links, 'horizontal')

  const positions = new Map<string, { x: number; y: number }>()
  for (const card of cards.values()) {
    if (card.node !== null) {
      positions.set(card.id, { x: snap(card.x), y: snap(card.y) })
    }
  }
  return positions
}
