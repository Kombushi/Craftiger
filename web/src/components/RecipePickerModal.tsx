import { useEffect, useState } from 'react'
import * as api from '../api'
import { fmtCost, fmtDuration } from '../format'
import { useStore } from '../storeContext'
import type { ItemDetail, PlannerStep, RecipeDto } from '../types'
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

const chancePct = (chance: number) => `${+(chance * 100).toFixed(1)}%`

/** Picks one producer of an item as a pipeline step — the full catalog, farm rows included,
 * each row a mini recipe card: the machine block, its inputs, and every output with its chance. */
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
            {detail.recipes.map((recipe) => (
              <li key={recipe.recipeId}>
                <RecipeRow
                  detail={detail}
                  recipe={recipe}
                  onPick={() =>
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
                />
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </div>
  )
}

function RecipeRow({ detail, recipe, onPick }: { detail: ItemDetail; recipe: RecipeDto; onPick: () => void }) {
  const { meta } = useStore()
  const machineItem = recipe.machineItemId != null ? detail.items[recipe.machineItemId] : undefined
  const chip = recipe.scope != null ? scopeChips[recipe.scope] : undefined

  const inputSlot = (slot: { itemId: string; amount: number }[], key: string, catalyst: boolean) => {
    const first = slot[0]
    const item = detail.items[first.itemId]
    return (
      <Slot
        key={key}
        size="sm"
        atlasIdx={item?.atlasIdx ?? -1}
        badge={first.amount > 1 ? String(first.amount) : undefined}
        tooltip={{
          name: item?.name ?? first.itemId,
          lines: [
            `${first.amount}× per run`,
            ...(slot.length > 1 ? [`one of ${slot.length} alternatives`] : []),
            ...(catalyst ? ['catalyst — never consumed'] : []),
          ],
        }}
      />
    )
  }

  return (
    <button type="button" className="picker-row picker-recipe" onClick={onPick}>
      <Slot
        size="sm"
        atlasIdx={machineItem?.atlasIdx ?? -1}
        tooltip={{ name: machineItem?.name ?? recipe.machine }}
      />
      <span className="picker-main">
        <span className="picker-title">
          {recipe.machine}
          {meta !== null ? <span className="tag tag-chip mono">{meta.tierNames[recipe.tier] ?? recipe.tier}</span> : null}
          {chip !== undefined ? <span className="tag tag-chip mono">{chip}</span> : null}
        </span>
        <span className="picker-sub mono">
          {fmtDuration(recipe.durationTicks)} · {recipe.euT.toLocaleString('en-US')} EU/t
        </span>
      </span>
      <span className="picker-io">
        {recipe.slots.map((slot, index) => inputSlot(slot, `in-${index}`, false))}
        {recipe.catalysts.map((slot, index) => inputSlot(slot, `cat-${index}`, true))}
        <span className="card-arrow-inline">▶</span>
        {recipe.outputs.map((output, index) => {
          const item = detail.items[output.itemId]
          return (
            <span key={`out-${index}`} className="picker-out">
              <Slot
                size="sm"
                atlasIdx={item?.atlasIdx ?? -1}
                badge={output.amount > 1 ? String(output.amount) : undefined}
                highlight
                tooltip={{
                  name: item?.name ?? output.itemId,
                  lines: [
                    `${output.amount}× per run`,
                    ...(output.chance < 1 ? [`${chancePct(output.chance)} chance`] : []),
                  ],
                }}
              />
              {output.chance < 1 ? <span className="picker-chance mono">{chancePct(output.chance)}</span> : null}
            </span>
          )
        })}
      </span>
      <span className="mono picker-cost">{fmtCost(recipe.candidateCost)}</span>
    </button>
  )
}
