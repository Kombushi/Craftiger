import { useMemo, useState } from 'react'
import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import { DerivedMaterials } from './DerivedMaterials'
import { CartPanel } from './CartPanel'
import { ChainGraph } from './ChainGraph'
import { GaragePanel } from './GaragePanel'
import { MaterialsGrid } from './MaterialsGrid'
import { SidebarLayout } from './SidebarLayout'
import { Slot } from './Slot'
import { Warnings } from './Warnings'

/** Sentinel for the Σ chain tab: every cart target combined into one plan. */
const ALL = '__all__'

export function CraftingPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const { cart, results, status, stale, calculate } = useStore()
  const [selected, setSelected] = useState<string | null>(null)
  const activeKey =
    selected === ALL && cart.length > 1
      ? ALL
      : selected !== null && selected !== ALL && cart.some((entry) => entry.itemId === selected)
        ? selected
        : (cart[0]?.itemId ?? null)
  const activeBom =
    results === null || activeKey === null
      ? null
      : activeKey === ALL
        ? results.cart
        : (results.perTarget[activeKey] ?? null)
  // Only the viewed chain's own targets are hidden from the derived lists: another
  // cart target genuinely inside this chain is a real intermediate of it.
  const excluded = useMemo(
    () => (activeKey === ALL ? cart.map((entry) => entry.itemId) : activeKey !== null ? [activeKey] : []),
    [activeKey, cart],
  )
  const totalCost = results
    ? cart.reduce<number | null>((sum, entry) => {
        const cost = results.cart.items[entry.itemId]?.cost
        if (sum === null || cost === null || cost === undefined) {
          return null
        }
        return sum + cost * entry.count
      }, 0)
    : null
  const selectionCost = (() => {
    if (activeKey === ALL) {
      return totalCost
    }
    const entry = cart.find((candidate) => candidate.itemId === activeKey)
    const cost = activeKey !== null ? results?.cart.items[activeKey]?.cost : null
    return entry === undefined || cost === null || cost === undefined ? null : cost * entry.count
  })()

  return (
    <SidebarLayout
      hidden={sidebarHidden}
      sidebar={
        <>
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
        </>
      }
    >
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
                <span className="mono results-total" title="Total cost of the selection">
                  ₴ {fmtCost(selectionCost)}
                </span>
              </header>
              {activeBom ? <MaterialsGrid bom={activeBom} /> : null}
            </section>
            {activeBom ? (
              <DerivedMaterials bom={activeBom} excluded={excluded} calcKey={results} />
            ) : null}
            <section className="results-section results-chain">
              <header className="panel-title results-head">
                <span>Crafting chain</span>
                <span className="chain-tabs">
                  {cart.length > 1 ? (
                    <button
                      type="button"
                      className={`chain-tab${activeKey === ALL ? ' chain-tab-active' : ''}`}
                      title="All targets combined"
                      onClick={() => setSelected(ALL)}
                    >
                      <span className="slot slot-lg slot-sum">Σ</span>
                    </button>
                  ) : null}
                  {cart.map((entry) => (
                    <button
                      key={entry.itemId}
                      type="button"
                      className={`chain-tab${entry.itemId === activeKey ? ' chain-tab-active' : ''}`}
                      title={entry.name}
                      onClick={() => setSelected(entry.itemId)}
                    >
                      <Slot size="lg" atlasIdx={entry.atlasIdx} badge={String(entry.count)} />
                    </button>
                  ))}
                </span>
              </header>
              {activeBom ? (
                <ChainGraph bom={activeBom} />
              ) : (
                <p className="hint">Select a target to see its chain.</p>
              )}
            </section>
          </>
        )}
    </SidebarLayout>
  )
}