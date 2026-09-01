import { useEffect, useState } from 'react'
import * as api from '../api'
import { fmtEuT, fmtRate } from '../format'
import { useStore } from '../storeContext'
import type { GeneratorCatalogResponse, PlannerStep } from '../types'
import { Slot } from './Slot'

interface Props {
  onPick: (step: PlannerStep) => void
  onClose: () => void
}

/** Picks one buildable generator line — block, fuel and mode — as a pipeline step. */
export function GeneratorPickerModal({ onPick, onClose }: Props) {
  const { garage, b, weights, meta } = useStore()
  const [catalog, setCatalog] = useState<GeneratorCatalogResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState('')

  useEffect(() => {
    let live = true
    api
      .factoryGenerators(garage, b, weights)
      .then((fetched) => {
        if (live) {
          setCatalog(fetched)
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
  }, [garage, b, weights])

  const folded = filter.trim().toLowerCase()
  const shown = (catalog?.lines ?? []).filter((line) => {
    if (folded === '') {
      return true
    }
    const block = catalog?.items[line.machineItemId]?.name ?? line.machineItemId
    const fuel = catalog?.items[line.fuelItemId]?.name ?? line.fuelItemId
    return `${block} ${fuel} ${line.variant ?? ''}`.toLowerCase().includes(folded)
  })

  return (
    <div className="overlay" onClick={onClose}>
      <div className="modal modal-wide" onClick={(event) => event.stopPropagation()}>
        <header className="modal-head">
          <span className="panel-title">Add a generator step</span>
          <button type="button" className="ghost-button" onClick={onClose}>
            ×
          </button>
        </header>
        <input
          className="search-input"
          type="text"
          placeholder="Filter by block or fuel…"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
        />
        {error !== null ? <p className="hint">{error}</p> : null}
        {catalog === null && error === null ? <p className="hint">Pricing the fuel lines…</p> : null}
        {catalog !== null && shown.length === 0 ? (
          <p className="hint">No buildable generator line matches.</p>
        ) : null}
        {catalog !== null ? (
          <ul className="picker-list">
            {shown.slice(0, 100).map((line) => {
              const block = catalog.items[line.machineItemId]
              const fuel = catalog.items[line.fuelItemId]
              const blockName = block?.name ?? line.machineItemId
              const fuelName = fuel?.name ?? line.fuelItemId
              return (
                <li key={line.id}>
                  <button
                    type="button"
                    className="picker-row"
                    onClick={() =>
                      onPick({
                        id: line.id,
                        label: `${blockName} · ${fuelName}`,
                        atlasIdx: block?.atlasIdx ?? -1,
                        machine: line.map,
                        machineItemId: null,
                        ocSteps: null,
                        scope: null,
                      })
                    }
                  >
                    <Slot atlasIdx={block?.atlasIdx ?? -1} size="sm" />
                    <span className="picker-main">
                      <span className="picker-title">
                        {blockName} · {fuelName}
                        {line.variant !== null ? ` · ${line.variant}` : ''}
                      </span>
                      <span className="picker-sub mono">
                        {meta?.tierNames[line.tier] ?? line.tier} ·{' '}
                        {fmtRate(line.fuelPerSecond, 'second', fuel?.isFluid ?? false)} fuel
                      </span>
                    </span>
                    <span className="mono picker-cost">+{fmtEuT(line.netEuT)}</span>
                  </button>
                </li>
              )
            })}
            {shown.length > 100 ? (
              <li className="hint">…{shown.length - 100} more — narrow the filter.</li>
            ) : null}
          </ul>
        ) : null}
      </div>
    </div>
  )
}
