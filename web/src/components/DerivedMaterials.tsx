import { useEffect, useMemo, useState } from 'react'
import { fmtAka, fmtAmount, fmtCost, fmtCount, fmtStacks } from '../format'
import { useStore } from '../storeContext'
import type { BomResponse } from '../types'
import { Slot } from './Slot'

interface Derived {
  itemId: string
  wholeAmount: number
  amount: number
}

/** Intermediates by distance from the leaves: level 1 is crafted straight from raw
 * materials, each further level from the ones before it. Mirrors the chain layout's
 * longest-path layering so the sections line up with the graph's columns. */
function levelsOf(bom: BomResponse, excluded: ReadonlySet<string>): Derived[][] {
  const level = new Map<string, number>()
  // A loop's seed node shares its item with a loop member: its unit adds to that item.
  const produced = new Map<string, { wholeAmount: number; amount: number }>()
  for (const node of bom.nodes) {
    const sum = produced.get(node.itemId) ?? { wholeAmount: 0, amount: 0 }
    produced.set(node.itemId, {
      wholeAmount: sum.wholeAmount + node.wholeAmount,
      amount: sum.amount + node.amount,
    })
  }
  // Nodes arrive targets-first in topological order, so the reverse walk sees
  // every input's level before the recipe that consumes it; inside a loop the
  // members take whichever comes first, which keeps the pass finite.
  for (const node of bom.nodes.toReversed()) {
    if (node.seed) {
      continue
    }
    let deepest = 0
    for (const input of node.inputsPerRun) {
      deepest = Math.max(deepest, level.get(input.itemId) ?? 0)
    }
    level.set(node.itemId, deepest + 1)
  }

  const groups: Derived[][] = []
  for (const [itemId, depth] of level) {
    if (excluded.has(itemId)) {
      continue
    }
    const sum = produced.get(itemId)!
    while (groups.length < depth) {
      groups.push([])
    }
    groups[depth - 1].push({ itemId, wholeAmount: sum.wholeAmount, amount: sum.amount })
  }
  while (groups.length > 0 && groups[groups.length - 1].length === 0) {
    groups.pop()
  }
  return groups
}

/** The craft's intermediates, revealed cumulatively up to the slider's level. The level
 * resets with each calculation and the grids follow the slider after a short debounce. */
export function DerivedMaterials({
  bom, excluded, calcKey,
}: {
  bom: BomResponse
  excluded: string[]
  calcKey: unknown
}) {
  const { openDetail } = useStore()
  const [selected, setSelected] = useState(0)
  const [shown, setShown] = useState(0)

  useEffect(() => {
    setSelected(0)
    setShown(0)
  }, [calcKey])

  useEffect(() => {
    const timer = setTimeout(() => setShown(selected), 300)
    return () => clearTimeout(timer)
  }, [selected])

  const groups = useMemo(() => levelsOf(bom, new Set(excluded)), [bom, excluded])
  const maxLevel = groups.length
  if (maxLevel === 0) {
    return null
  }
  const visible = Math.min(shown, maxLevel)

  return (
    <section className="results-section">
      <header className="panel-title results-head">
        <span>Derived materials</span>
        <span className="derived-stepper">
          <span className="derived-stepper-label">up to level</span>
          <input
            type="range"
            className="derived-range"
            min={0}
            max={maxLevel}
            step={1}
            value={Math.min(selected, maxLevel)}
            onChange={(event) => setSelected(Number(event.target.value))}
          />
          <span className="derived-stepper-label mono">
            {Math.min(selected, maxLevel)} / {maxLevel}
          </span>
        </span>
      </header>
      {groups.slice(0, visible).map((group, index) => (
        <div key={index} className="derived-level">
          <header className="derived-level-title mono">Level {index + 1}</header>
          <div className="materials">
            {group
              .toSorted((a, b) => {
                const fluidA = bom.items[a.itemId]?.isFluid ? 1 : 0
                const fluidB = bom.items[b.itemId]?.isFluid ? 1 : 0
                if (fluidA !== fluidB) {
                  return fluidA - fluidB
                }
                const costA = (bom.items[a.itemId]?.cost ?? 0) * a.wholeAmount
                const costB = (bom.items[b.itemId]?.cost ?? 0) * b.wholeAmount
                return costB - costA
              })
              .map((derived) => {
                const item = bom.items[derived.itemId]
                if (!item) {
                  return null
                }
                const total = item.cost === null ? null : item.cost * derived.wholeAmount
                const stacks = item.isFluid ? null : fmtStacks(derived.wholeAmount, item.maxStack)
                return (
                  <Slot
                    key={derived.itemId}
                    size="lg"
                    atlasIdx={item.atlasIdx}
                    badge={fmtCount(derived.wholeAmount)}
                    tooltip={{
                      name: fmtAka(item, derived.itemId),
                      lines: [
                        `${fmtAmount(derived.wholeAmount, item.isFluid)} produced (${fmtCount(derived.amount)} expected)`,
                        ...(stacks ? [stacks] : []),
                        `${fmtCost(item.cost)} each · ${fmtCost(total)} total`,
                      ],
                    }}
                    onClick={() => openDetail(derived.itemId)}
                  />
                )
              })}
          </div>
        </div>
      ))}
    </section>
  )
}
