import { useRef, useState } from 'react'
import type { CSSProperties, PointerEvent as ReactPointerEvent } from 'react'
import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import { usePersistent } from '../usePersistent'
import { CartPanel } from './CartPanel'
import { ChainGraph } from './ChainGraph'
import { GaragePanel } from './GaragePanel'
import { MaterialsGrid } from './MaterialsGrid'
import { Slot } from './Slot'
import { Warnings } from './Warnings'

const SIDEBAR_MIN = 280
const SIDEBAR_MAX = 640
const SIDEBAR_DEFAULT = 380

const clampSidebar = (width: number) => Math.min(SIDEBAR_MAX, Math.max(SIDEBAR_MIN, width))

export function PlannerPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const { cart, results, status, stale, calculate } = useStore()
  const [selected, setSelected] = useState<string | null>(null)
  const plannerRef = useRef<HTMLDivElement | null>(null)
  const [sidebarWidth, setSidebarWidth] = usePersistent('gtnhp.sidebarWidth', SIDEBAR_DEFAULT)

  // The drag writes the CSS variable directly so the chain graph is not re-rendered
  // per pointer move; React state catches up once on release.
  const dragSidebar = (event: ReactPointerEvent<HTMLDivElement>) => {
    const planner = plannerRef.current
    if (planner === null) {
      return
    }
    event.preventDefault()
    const handle = event.currentTarget
    handle.setPointerCapture(event.pointerId)
    const left = planner.getBoundingClientRect().left
    document.body.style.cursor = 'col-resize'
    let width = sidebarWidth
    const move = (moveEvent: PointerEvent) => {
      width = clampSidebar(Math.round(moveEvent.clientX - left))
      planner.style.setProperty('--sidebar-width', `${width}px`)
    }
    const stop = () => {
      handle.removeEventListener('pointermove', move)
      handle.removeEventListener('pointerup', stop)
      handle.removeEventListener('pointercancel', stop)
      document.body.style.cursor = ''
      setSidebarWidth(width)
    }
    handle.addEventListener('pointermove', move)
    handle.addEventListener('pointerup', stop)
    handle.addEventListener('pointercancel', stop)
  }

  const resetSidebar = () => {
    plannerRef.current?.style.removeProperty('--sidebar-width')
    setSidebarWidth(SIDEBAR_DEFAULT)
  }
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
    <div
      ref={plannerRef}
      className={`planner${sidebarHidden ? ' planner-collapsed' : ''}`}
      style={{ '--sidebar-width': `${sidebarWidth}px` } as CSSProperties}
    >
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
      </aside>
      <div
        className="sidebar-handle"
        title="Drag to resize; double-click to reset"
        onPointerDown={dragSidebar}
        onDoubleClick={resetSidebar}
      />
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
                      <Slot atlasIdx={entry.atlasIdx} badge={String(entry.count)} />
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