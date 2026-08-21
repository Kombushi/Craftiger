import { fmtAka, fmtAmount, fmtCost, fmtCount, fmtStacks } from '../format'
import { useStore } from '../storeContext'
import type { BomResponse } from '../types'

import { Slot } from './Slot'

export function MaterialsGrid({ bom }: { bom: BomResponse }) {
  const { openDetail } = useStore()
  // Fluids close the list; solids sort by what they cost the plan.
  const leaves = bom.leaves.toSorted((a, b) => {
    const fluidA = bom.items[a.itemId]?.isFluid ? 1 : 0
    const fluidB = bom.items[b.itemId]?.isFluid ? 1 : 0
    if (fluidA !== fluidB) {
      return fluidA - fluidB
    }
    const costA = (bom.items[a.itemId]?.cost ?? 0) * a.wholeAmount
    const costB = (bom.items[b.itemId]?.cost ?? 0) * b.wholeAmount
    return costB - costA
  })

  if (leaves.length === 0) {
    return <p className="hint">No raw materials — nothing reachable to expand.</p>
  }

  return (
    <div className="materials">
      {leaves.map((leaf) => {
        const item = bom.items[leaf.itemId]
        if (!item) {
          return null
        }
        const total = item.cost === null ? null : item.cost * leaf.wholeAmount
        const stacks = item.isFluid ? null : fmtStacks(leaf.wholeAmount, item.maxStack)
        return (
          <Slot
            key={leaf.itemId}
            size="lg"
            atlasIdx={item.atlasIdx}
            badge={fmtCount(leaf.wholeAmount)}
            tooltip={{
              name: fmtAka(item, leaf.itemId),
              lines: [
                `${fmtAmount(leaf.wholeAmount, item.isFluid)} to gather (${fmtCount(leaf.amount)} expected)`,
                ...(stacks ? [stacks] : []),
                `${fmtCost(item.cost)} each · ${fmtCost(total)} total`,
              ],
            }}
            onClick={() => openDetail(leaf.itemId)}
          />
        )
      })}
    </div>
  )
}