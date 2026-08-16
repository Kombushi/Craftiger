import { fmtAmount, fmtCost, fmtCount } from '../format'
import { useStore } from '../storeContext'
import type { BomResponse } from '../types'

import { Slot } from './Slot'

export function MaterialsGrid({ bom }: { bom: BomResponse }) {
  const { openDetail } = useStore()
  const leaves = bom.leaves.toSorted((a, b) => {
    const costA = (bom.items[a.itemId]?.cost ?? 0) * a.amount
    const costB = (bom.items[b.itemId]?.cost ?? 0) * b.amount
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
        const total = item.cost === null ? null : item.cost * leaf.amount
        return (
          <Slot
            key={leaf.itemId}
            atlasIdx={item.atlasIdx}
            badge={fmtCount(leaf.amount)}
            title={`${item.name}\n${fmtAmount(leaf.amount, item.isFluid)} · ${fmtCost(item.cost)} each · ${fmtCost(total)} total`}
            onClick={() => openDetail(leaf.itemId)}
          />
        )
      })}
    </div>
  )
}