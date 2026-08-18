import { useEffect, useState } from 'react'
import * as api from '../api'
import { ApiError } from '../api'
import { fmtAmount, fmtCost, fmtCount, fmtDuration, fmtHeat } from '../format'
import { useStore } from '../storeContext'
import type { ItemDetail, RecipeDto, SlotAlternative } from '../types'
import { Slot } from './Slot'

export function ItemDetailOverlay() {
  const { detailItemId, closeDetail, results, meta, pins, setPin, addToCart, cart } = useStore()
  const [detail, setDetail] = useState<ItemDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const solveId = results?.solveId ?? null

  useEffect(() => {
    setDetail(null)
    setError(null)
    if (!detailItemId || !solveId) {
      return
    }
    let live = true
    api
      .itemDetail(detailItemId, solveId)
      .then((response) => {
        if (live) {
          setDetail(response)
        }
      })
      .catch((caught: unknown) => {
        if (live) {
          setError(
            caught instanceof ApiError && caught.status === 404
              ? 'This solve expired — press Calculate again.'
              : caught instanceof Error
                ? caught.message
                : String(caught),
          )
        }
      })
    return () => {
      live = false
    }
  }, [detailItemId, solveId])

  useEffect(() => {
    if (!detailItemId) {
      return
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeDetail()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [detailItemId, closeDetail])

  if (!detailItemId) {
    return null
  }

  return (
    <div className="overlay" onClick={closeDetail}>
      <div className="modal modal-wide" onClick={(event) => event.stopPropagation()}>
        {!solveId ? (
          <>
            <header className="modal-head">
              <span className="panel-title">Item</span>
              <button type="button" className="ghost-button" onClick={closeDetail}>
                ×
              </button>
            </header>
            <p className="hint">Recipes need prices — press Calculate first.</p>
          </>
        ) : error ? (
          <>
            <header className="modal-head">
              <span className="panel-title">Item</span>
              <button type="button" className="ghost-button" onClick={closeDetail}>
                ×
              </button>
            </header>
            <p className="warning-row">{error}</p>
          </>
        ) : !detail ? (
          <p className="hint">Loading…</p>
        ) : (
          <>
            <header className="modal-head">
              <Slot atlasIdx={detail.atlasIdx} />
              <span className="detail-title">
                <span className="detail-name">{detail.name}</span>
                <span className="detail-sub mono">
                  {fmtCost(detail.cost)}
                  {detail.leafClass ? ` · ${detail.leafClass}` : ''}
                </span>
              </span>
              {cart.some((entry) => entry.itemId === detail.itemId) ? null : (
                <button
                  type="button"
                  className="action-button"
                  onClick={() =>
                    addToCart({
                      itemId: detail.itemId,
                      name: detail.name,
                      atlasIdx: detail.atlasIdx,
                      isFluid: false,
                    })
                  }
                >
                  Add to list
                </button>
              )}
              <button type="button" className="ghost-button" onClick={closeDetail}>
                ×
              </button>
            </header>
            {detail.leafClass !== null ? (
              <p className="hint">
                A leaf material — priced at its weight, never expanded into a recipe.
              </p>
            ) : null}
            {detail.recipes.length === 0 ? (
              <p className="hint">No garage-legal recipe produces this item.</p>
            ) : (
              <ul className="detail-recipes">
                {detail.recipes.map((recipe) => (
                  <DetailRecipe
                    key={recipe.recipeId}
                    recipe={recipe}
                    detail={detail}
                    tierNames={meta?.tierNames ?? []}
                    best={recipe.recipeId === detail.bestRecipeId}
                    pinned={pins[detail.itemId] === recipe.recipeId}
                    onPin={(pin) => setPin(detail.itemId, pin ? recipe.recipeId : null)}
                  />
                ))}
              </ul>
            )}
          </>
        )}
      </div>
    </div>
  )
}

interface DetailRecipeProps {
  recipe: RecipeDto
  detail: ItemDetail
  tierNames: string[]
  best: boolean
  pinned: boolean
  onPin: (pin: boolean) => void
}

function cheapest(slot: SlotAlternative[]): SlotAlternative {
  return slot.reduce((chosen, alternative) => {
    const chosenCost = chosen.cost ?? Number.POSITIVE_INFINITY
    const cost = alternative.cost ?? Number.POSITIVE_INFINITY
    return cost < chosenCost ? alternative : chosen
  })
}

function altLines(slot: SlotAlternative[], name: (id: string) => string): string {
  if (slot.length <= 1) {
    return ''
  }
  const lines = slot
    .slice(0, 8)
    .map((alternative) => `  ${name(alternative.itemId)}`)
    .join('\n')
  return `\n${slot.length} alternatives:\n${lines}${slot.length > 8 ? '\n  …' : ''}`
}

function DetailRecipe({ recipe, detail, tierNames, best, pinned, onPin }: DetailRecipeProps) {
  const { openDetail } = useStore()
  const item = (id: string) => detail.items[id]
  return (
    <li className={`detail-recipe${best ? ' detail-best' : ''}${pinned ? ' detail-pinned' : ''}`}>
      <header className="card-head">
        <span className="card-machine" title={recipe.machine}>
          {recipe.machine}
        </span>
        <span className="card-tier mono">
          {tierNames[recipe.tier] ?? recipe.tier}
          {recipe.heat !== null ? ` · ${fmtHeat(recipe.heat)}` : ''}
        </span>
        {best ? <span className="tag tag-best">BEST</span> : null}
        {pinned ? <span className="tag tag-pinned">PINNED</span> : null}
        <span className="card-runs mono">{fmtCost(recipe.candidateCost)}</span>
      </header>
      <div className="detail-body">
        <div className="detail-slots">
          {recipe.slots.map((slot, index) => {
            const chosen = cheapest(slot)
            const chosenItem = item(chosen.itemId)
            const alternatives = altLines(slot, (id) => item(id)?.name ?? id)
            return (
              <span key={index} className="detail-slot">
                <Slot
                  atlasIdx={chosenItem?.atlasIdx ?? -1}
                  badge={fmtCount(chosen.amount)}
                  title={`${chosenItem?.name ?? chosen.itemId} · ${fmtAmount(chosen.amount, chosenItem?.isFluid ?? false)} · ${fmtCost(chosenItem?.cost ?? null)} each${alternatives}`}
                  onClick={() => openDetail(chosen.itemId)}
                />
                {slot.length > 1 ? <span className="alt-badge mono">+{slot.length - 1}</span> : null}
              </span>
            )
          })}
          {recipe.catalysts.map((slot, index) => {
            const tool = slot[0]
            const toolItem = item(tool.itemId)
            const alternatives = altLines(slot, (id) => item(id)?.name ?? id)
            return (
              <span key={`tool-${index}`} className="detail-slot">
                <Slot
                  atlasIdx={toolItem?.atlasIdx ?? -1}
                  badge={fmtCount(tool.amount)}
                  dim
                  title={`${toolItem?.name ?? tool.itemId} · needed in place — not consumed${alternatives}`}
                  onClick={() => openDetail(tool.itemId)}
                />
                {slot.length > 1 ? <span className="alt-badge mono">+{slot.length - 1}</span> : null}
              </span>
            )
          })}
        </div>
        <span className="card-arrow-inline">▶</span>
        <div className="detail-slots">
          {recipe.outputs.map((output, index) => {
            const outputItem = item(output.itemId)
            return (
              <Slot
                key={index}
                atlasIdx={outputItem?.atlasIdx ?? -1}
                badge={String(output.amount)}
                dim={output.itemId !== detail.itemId}
                title={`${outputItem?.name ?? output.itemId}${output.chance < 1 ? ` · ${Math.round(output.chance * 100)}%` : ''}`}
                onClick={() => openDetail(output.itemId)}
              />
            )
          })}
        </div>
        <span className="detail-meta mono">
          {fmtDuration(recipe.durationTicks)} · {recipe.euT.toLocaleString('en-US')} EU/t
        </span>
        <button
          type="button"
          className={`pin-button${pinned ? ' pin-active' : ''}`}
          onClick={() => onPin(!pinned)}
        >
          {pinned ? 'UNPIN' : 'PIN'}
        </button>
      </div>
    </li>
  )
}