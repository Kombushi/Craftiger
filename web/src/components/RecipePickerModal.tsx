import { useEffect, useState } from 'react'
import * as api from '../api'
import { fmtCost, fmtDuration } from '../format'
import { useStore } from '../storeContext'
import type { ItemDetail, PlannerStep } from '../types'
import { Slot } from './Slot'

interface Props {
  itemId: string
  onPick: (step: PlannerStep) => void
  onClose: () => void
}

/** How a factory-only scope reads on a picker row. */
const scopeChips: Record<string, string> = {
  factory: 'FARM',
  factory_mob: 'MOB',
  factory_bred: 'BRED',
}

/** Picks one producer of an item as a pipeline step — the full catalog, farm rows included. */
export function RecipePickerModal({ itemId, onPick, onClose }: Props) {
  const { garage, b, weights } = useStore()
  const [detail, setDetail] = useState<ItemDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    api
      .factoryProducers(garage, b, weights, itemId)
      .then((fetched) => {
        if (live) {
          setDetail(fetched)
        }
      })
      .catch((failure: unknown) => {
        if (live) {
          setError(failure instanceof Error ? failure.message : String(failure))
        }
      })
    return () => {
      live = false
    }
  }, [itemId, garage, b, weights])

  return (
    <div className="overlay" onClick={onClose}>
      <div className="modal modal-wide" onClick={(event) => event.stopPropagation()}>
        <header className="modal-head">
          <span className="panel-title">
            Add a step making {detail?.name ?? '…'}
          </span>
          <button type="button" className="ghost-button" onClick={onClose}>
            ×
          </button>
        </header>
        {error !== null ? <p className="hint">{error}</p> : null}
        {detail === null && error === null ? <p className="hint">Loading recipes…</p> : null}
        {detail !== null && detail.recipes.length === 0 ? (
          <p className="hint">No garage-legal recipe produces this item.</p>
        ) : null}
        {detail !== null ? (
          <ul className="picker-list">
            {detail.recipes.map((recipe) => {
              const outputs = recipe.outputs
                .map((output) => {
                  const name = detail.items[output.itemId]?.name ?? output.itemId
                  return `${output.amount}× ${name}`
                })
                .join(', ')
              const chip = recipe.scope != null ? scopeChips[recipe.scope] : undefined
              return (
                <li key={recipe.recipeId}>
                  <button
                    type="button"
                    className="picker-row"
                    onClick={() =>
                      onPick({
                        id: recipe.recipeId,
                        label: detail.name,
                        atlasIdx: detail.atlasIdx,
                        machine: recipe.machine,
                        machineItemId: null,
                        ocSteps: null,
                        scope: recipe.scope ?? null,
                      })
                    }
                  >
                    <Slot atlasIdx={detail.atlasIdx} size="sm" />
                    <span className="picker-main">
                      <span className="picker-title">
                        {recipe.machine}
                        {chip !== undefined ? <span className="tag tag-chip mono"> {chip}</span> : null}
                      </span>
                      <span className="picker-sub mono">
                        {fmtDuration(recipe.durationTicks)} · {recipe.euT.toLocaleString('en-US')} EU/t
                        {' → '}
                        {outputs}
                      </span>
                    </span>
                    <span className="mono picker-cost">{fmtCost(recipe.candidateCost)}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        ) : null}
      </div>
    </div>
  )
}
