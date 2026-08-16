import { useState } from 'react'
import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import { CartPanel } from './CartPanel'
import { ChainGraph } from './ChainGraph'
import { GaragePanel } from './GaragePanel'
import { MaterialsGrid } from './MaterialsGrid'
import { Slot } from './Slot'
import { Warnings } from './Warnings'

export function PlannerPage() {
  const { cart, results, status, stale, calculate } = useStore()
  const [selected, setSelected] = useState<string | null>(null)
  const activeTarget =
    selected !== null && cart.some((entry) => entry.itemId === selected)
      ? selected
      : (cart[0]?.itemId ?? null)
  const chain = results && activeTarget ? results.perTarget[activeTarget] : null

  const totalCost = results
    ? cart.reduce<number | null>((sum, entry) => {
        const cost = results.cart.items[entry.itemId]?.cost
        if (sum === null || cost === null || cost === undefined) {
          return null
        }
        return sum + cost * entry.count
      }, 0)
    : null

  return (
    <div className="planner">
      <aside className="sidebar">
        <CartPanel />
        <GaragePanel />
        <button
          type="button"
          className="calculate"
          disabled={cart.length === 0 || status.phase === 'solving'}
          onClick={calculate}
        >
          {status.phase === 'solving' ? 'CALCULATING…' : stale ? 'RECALCULATE' : 'CALCULATE'}
        </button>
        {status.phase === 'error' ? <p className="warning-row">{status.message}</p> : null}
      </aside>
      <main className="results">
        {results === null ? (
          <div className="results-empty">
            <p className="hint">
              {status.phase === 'solving'
                ? 'Pricing every item under your garage…'
                : 'Pick items, set up the garage, then press CALCULATE.'}
            </p>
          </div>
        ) : (
          <>
            {stale ? (
              <p className="stale-banner">Settings changed — showing the last calculation.</p>
            ) : null}
            <Warnings bom={results.cart} />
            <section className="results-section">
              <header className="panel-title results-head">
                <span>Raw materials</span>
                <span className="mono results-total" title="Total cost of the whole craft list">
                  Σ {fmtCost(totalCost)}
                </span>
              </header>
              <MaterialsGrid bom={results.cart} />
            </section>
            <section className="results-section results-chain">
              <header className="panel-title results-head">
                <span>Crafting chain</span>
                <span className="chain-tabs">
                  {cart.map((entry) => (
                    <button
                      key={entry.itemId}
                      type="button"
                      className={`chain-tab${entry.itemId === activeTarget ? ' chain-tab-active' : ''}`}
                      title={entry.name}
                      onClick={() => setSelected(entry.itemId)}
                    >
                      <Slot atlasIdx={entry.atlasIdx} size="sm" badge={String(entry.count)} />
                    </button>
                  ))}
                </span>
              </header>
              {chain ? (
                <ChainGraph bom={chain} />
              ) : (
                <p className="hint">Select a target to see its chain.</p>
              )}
            </section>
          </>
        )}
      </main>
    </div>
  )
}