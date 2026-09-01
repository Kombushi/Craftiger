import {
  LEAF_H,
  LEAF_W,
  arrange,
  bodySize,
  type ChainEdge,
  type ChainOrientation,
  type LayoutLink,
} from './chainLayout'
import type { FactoryLine, FactoryResponse } from './types'

/** A flow-graph card: one running machine line, or one stream entering from outside —
 * a purchased or auto-infinite inflow ('input') or a consume target's intake ('intake'). */
export interface FactoryCard {
  id: string
  kind: 'line' | 'input' | 'intake'
  line: FactoryLine | null
  itemId: string | null
  /** Members of a multi-card strongly connected component carry the LOOP tag. */
  loop: boolean
  inCols: number
  outCols: number
  x: number
  y: number
  w: number
  h: number
}

export interface FactoryLayout {
  cards: FactoryCard[]
  edges: ChainEdge[]
  width: number
  height: number
}

/** What an EST chip owns up to, per flag. */
export function estimateNote(line: { estimated: boolean; durationless: boolean }): string | null {
  if (line.durationless) {
    return 'Durationless: a free instant converter — no machine time or EU is modeled for it.'
  }
  if (line.estimated) {
    return 'Estimated: this machine has no curated bonus data, so it runs without its multiblock bonuses.'
  }
  return null
}

function lineInputs(line: FactoryLine) {
  return line.inputs ?? []
}

function lineOutputs(line: FactoryLine) {
  return line.outputs ?? []
}

/** Lays the plan out as one steady-state network: a card per machine line, source cards for
 * the streams entering from outside, edges fanning out from every producer of an item, and
 * layers from SCC condensation ordered by longest path — a loop's members share a layer. */
export function layoutFactory(plan: FactoryResponse, orientation: ChainOrientation): FactoryLayout {
  const cards = new Map<string, FactoryCard>()
  plan.lines.forEach((line, index) => {
    const inputs = lineInputs(line)
    const outputs = lineOutputs(line)
    const inCols = Math.min(4, Math.max(1, inputs.length))
    const outCols = Math.min(2, Math.max(1, outputs.length))
    const { w, h } = bodySize(
      inCols,
      Math.max(1, Math.ceil(inputs.length / inCols)),
      outCols,
      Math.max(1, Math.ceil(outputs.length / outCols)),
    )
    cards.set(`line:${index}`, {
      id: `line:${index}`,
      kind: 'line',
      line,
      itemId: null,
      loop: false,
      inCols,
      outCols,
      x: 0,
      y: 0,
      w,
      h,
    })
  })

  const consumers = new Map<string, { card: string; slot: number; inCols: number }[]>()
  const producers = new Map<string, string[]>()
  plan.lines.forEach((line, index) => {
    lineInputs(line).forEach((flow, slot) => {
      consumers.set(flow.itemId, [
        ...(consumers.get(flow.itemId) ?? []),
        { card: `line:${index}`, slot, inCols: cards.get(`line:${index}`)!.inCols },
      ])
    })
    for (const flow of lineOutputs(line)) {
      producers.set(flow.itemId, [...(producers.get(flow.itemId) ?? []), `line:${index}`])
    }
  })

  const source = (id: string, kind: 'input' | 'intake', itemId: string) => {
    cards.set(id, {
      id, kind, line: null, itemId, loop: false, inCols: 0, outCols: 0, x: 0, y: 0, w: LEAF_W, h: LEAF_H,
    })
    producers.set(itemId, [...(producers.get(itemId) ?? []), id])
  }
  for (const inflow of plan.inflows) {
    if (consumers.has(inflow.itemId)) {
      source(`in:${inflow.itemId}`, 'input', inflow.itemId)
    }
  }
  for (const flow of plan.flows) {
    if (flow.supplied > 0 && consumers.has(flow.itemId)) {
      source(`sup:${flow.itemId}`, 'intake', flow.itemId)
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

  const componentOf = condense(cards.keys(), links)
  const componentSize = new Map<number, number>()
  for (const component of componentOf.values()) {
    componentSize.set(component, (componentSize.get(component) ?? 0) + 1)
  }
  for (const card of cards.values()) {
    card.loop = (componentSize.get(componentOf.get(card.id)!) ?? 0) > 1
  }
  for (const link of links) {
    link.loop =
      componentOf.get(link.from) === componentOf.get(link.to) &&
      (componentSize.get(componentOf.get(link.from)!) ?? 0) > 1
  }

  // Longest path over the condensation: every card of a component shares its layer.
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

  const { edges, width, height } = arrange(layers, cards, links, orientation)
  return { cards: [...cards.values()], edges, width, height }
}

/** Tarjan's strongly connected components, iteratively — plans are small but recursion depth is not worth risking. */
export function condense(ids: Iterable<string>, links: LayoutLink[]): Map<string, number> {
  const adjacency = new Map<string, string[]>()
  for (const link of links) {
    adjacency.set(link.from, [...(adjacency.get(link.from) ?? []), link.to])
  }
  const index = new Map<string, number>()
  const low = new Map<string, number>()
  const onStack = new Set<string>()
  const stack: string[] = []
  const componentOf = new Map<string, number>()
  let next = 0
  let components = 0

  for (const start of ids) {
    if (index.has(start)) {
      continue
    }
    const work: { id: string; edge: number }[] = [{ id: start, edge: 0 }]
    index.set(start, next)
    low.set(start, next)
    next++
    stack.push(start)
    onStack.add(start)
    while (work.length > 0) {
      const frame = work[work.length - 1]
      const targets = adjacency.get(frame.id) ?? []
      if (frame.edge < targets.length) {
        const to = targets[frame.edge]
        frame.edge++
        if (!index.has(to)) {
          index.set(to, next)
          low.set(to, next)
          next++
          stack.push(to)
          onStack.add(to)
          work.push({ id: to, edge: 0 })
        } else if (onStack.has(to)) {
          low.set(frame.id, Math.min(low.get(frame.id)!, index.get(to)!))
        }
        continue
      }
      work.pop()
      if (work.length > 0) {
        const parent = work[work.length - 1]
        low.set(parent.id, Math.min(low.get(parent.id)!, low.get(frame.id)!))
      }
      if (low.get(frame.id) === index.get(frame.id)) {
        for (;;) {
          const member = stack.pop()!
          onStack.delete(member)
          componentOf.set(member, components)
          if (member === frame.id) {
            break
          }
        }
        components++
      }
    }
  }
  return componentOf
}
