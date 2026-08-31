import { useRef } from 'react'
import type { CSSProperties, PointerEvent as ReactPointerEvent } from 'react'
import { useFactory } from '../factoryContext'
import { fmtAka, fmtCost, fmtEuT, fmtRate, fmtRateBadge } from '../format'
import { useStore } from '../storeContext'
import type { FactoryResponse, RateUnit } from '../types'
import { usePersistent } from '../usePersistent'
import { estimateNote } from '../factoryLayout'
import { FactoryGraph } from './FactoryGraph'
import { FactoryTargetsPanel } from './FactoryTargetsPanel'
import { FactoryWarnings } from './FactoryWarnings'
import { GaragePanel } from './GaragePanel'
import { Slot } from './Slot'

const SIDEBAR_MIN = 280
const SIDEBAR_MAX = 640
const SIDEBAR_DEFAULT = 380

const clampSidebar = (width: number) => Math.min(SIDEBAR_MAX, Math.max(SIDEBAR_MIN, width))

const statusNotes: Record<string, string> = {
  infeasible: 'No feasible plan — the warnings above say why.',
  unbounded: 'The model is unbounded — a data defect, not a plan; see the warning.',
  timed_out: 'The solve hit its time budget before finishing.',
  failed: 'The solve failed before producing a plan.',
}

export function FactoryPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const { targets, plan, status, stale, solve } = useFactory()
  const [unit, setUnit] = usePersistent<RateUnit>('gtnhp.rateUnit', 'second')
  const plannerRef = useRef<HTMLDivElement | null>(null)
  const [sidebarWidth, setSidebarWidth] = usePersistent('gtnhp.sidebarWidth', SIDEBAR_DEFAULT)

  // The drag writes the CSS variable directly so the flow graph is not re-rendered
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

  return (
    <div
      ref={plannerRef}
      className={`planner${sidebarHidden ? ' planner-collapsed' : ''}`}
      style={{ '--sidebar-width': `${sidebarWidth}px` } as CSSProperties}
    >
      <aside className="sidebar">
        <FactoryTargetsPanel />
        <GaragePanel
          targetIds={targets.flatMap((target) => (target.kind === 'energy' ? [] : [target.itemId]))}
        />
        <button
          type="button"
          className="calculate"
          disabled={targets.length === 0 || status.phase === 'solving'}
          onClick={solve}
        >
          {status.phase === 'solving' ? 'SOLVING…' : stale ? 'RESOLVE' : 'SOLVE'}
        </button>
      </aside>
      <div
        className="sidebar-handle"
        title="Drag to resize; double-click to reset"
        onPointerDown={dragSidebar}
        onDoubleClick={() => {
          plannerRef.current?.style.removeProperty('--sidebar-width')
          setSidebarWidth(SIDEBAR_DEFAULT)
        }}
      />
      <main className="results">
        {plan === null ? (
          <div className="results-empty">
            <p className="hint">
              {status.phase === 'solving'
                ? 'Solving the steady state…'
                : 'Pick rate targets, set up the garage, then press SOLVE.'}
            </p>
          </div>
        ) : (
          <>
            {stale ? (
              <p className="stale-banner">Settings changed — showing the last plan.</p>
            ) : null}
            <FactoryWarnings plan={plan} />
            {plan.status !== 'solved' ? (
              <div className="results-empty">
                <p className="hint">{statusNotes[plan.status] ?? plan.status}</p>
              </div>
            ) : (
              <SolvedPlan plan={plan} unit={unit} setUnit={setUnit} />
            )}
          </>
        )}
      </main>
    </div>
  )
}

function SolvedPlan({
  plan,
  unit,
  setUnit,
}: {
  plan: FactoryResponse
  unit: RateUnit
  setUnit: (unit: RateUnit) => void
}) {
  const { openDetail } = useStore()
  const machineCount = plan.lines.reduce((sum, line) => sum + Math.ceil(line.busyMachines), 0)
  const assumed = plan.lines.filter((line) => estimateNote(line) !== null).length
  const surplus = plan.flows
    .filter((flow) => flow.surplus > 1e-6)
    .toSorted((a, b) => b.surplus - a.surplus)
  const intake = plan.flows.filter((flow) => flow.supplied > 1e-9)

  return (
    <>
      <section className="results-section">
        <header className="panel-title results-head">
          <span>Steady state</span>
          <span className="seg unit-picker" title="Display unit for every rate">
            {(['tick', 'second', 'minute'] as const).map((option) => (
              <button
                key={option}
                type="button"
                className={unit === option ? 'seg-active' : ''}
                onClick={() => setUnit(option)}
              >
                {option === 'tick' ? '/t' : option === 'second' ? '/s' : '/min'}
              </button>
            ))}
          </span>
        </header>
        <div className="factory-totals mono">
          <span title="Purchased leaf inflows at their weights">₴ {fmtRate(plan.pricedInflowCost, unit)}</span>
          <span title="Total machine draw">{fmtEuT(plan.drawEuT)} draw</span>
          {plan.exportEuT > 0 ? (
            <span
              title={`The generators emit ${Math.round(plan.exportEuT).toLocaleString('en-US')} EU/t; the plan's own draw comes out of it first`}
            >
              +{fmtEuT(plan.exportEuT - plan.drawEuT)} net
            </span>
          ) : null}
          <span title={`${plan.busyMachines.toFixed(2)} continuously busy; the count sums each line's whole machines`}>
            {machineCount}× machines
          </span>
        </div>
        {assumed > 0 ? (
          <p className="est-banner">
            {assumed} line{assumed === 1 ? ' runs' : 's run'} on assumptions — the EST chips say what
            is assumed.
          </p>
        ) : null}
      </section>
      <section className="results-section">
        <header className="panel-title results-head">
          <span>Inputs</span>
        </header>
        {plan.inflows.length === 0 && intake.length === 0 ? (
          <p className="hint">Nothing flows in — the plan feeds itself.</p>
        ) : (
          <div className="materials">
            {plan.inflows.map((inflow) => {
              const item = plan.items[inflow.itemId]
              return (
                <Slot
                  key={inflow.itemId}
                  size="lg"
                  atlasIdx={item?.atlasIdx ?? -1}
                  badge={fmtRateBadge(inflow.rate, unit)}
                  needBadge={inflow.autoInfinite ? '∞' : undefined}
                  tooltip={{
                    name: fmtAka(item, inflow.itemId),
                    lines: [
                      `${fmtRate(inflow.rate, unit, item?.isFluid ?? false)} in`,
                      inflow.autoInfinite
                        ? 'auto-infinite — free under this garage'
                        : `${fmtCost(inflow.weight)} each`,
                    ],
                  }}
                  onClick={() => openDetail(inflow.itemId)}
                />
              )
            })}
            {intake.map((flow) => {
              const item = plan.items[flow.itemId]
              return (
                <Slot
                  key={`sup-${flow.itemId}`}
                  size="lg"
                  atlasIdx={item?.atlasIdx ?? -1}
                  badge={fmtRateBadge(flow.supplied, unit)}
                  tooltip={{
                    name: fmtAka(item, flow.itemId),
                    lines: [`${fmtRate(flow.supplied, unit, item?.isFluid ?? false)} intake absorbed`],
                  }}
                  onClick={() => openDetail(flow.itemId)}
                />
              )
            })}
          </div>
        )}
      </section>
      {surplus.length > 0 ? (
        <section className="results-section">
          <header className="panel-title results-head">
            <span>Byproducts &amp; surplus</span>
          </header>
          <div className="materials">
            {surplus.map((flow) => {
              const item = plan.items[flow.itemId]
              return (
                <Slot
                  key={flow.itemId}
                  size="lg"
                  atlasIdx={item?.atlasIdx ?? -1}
                  badge={`+${fmtRateBadge(flow.surplus, unit)}`}
                  tooltip={{
                    name: fmtAka(item, flow.itemId),
                    lines: [
                      `${fmtRate(flow.surplus, unit, item?.isFluid ?? false)} beyond what the plan uses`,
                    ],
                  }}
                  onClick={() => openDetail(flow.itemId)}
                />
              )
            })}
          </div>
        </section>
      ) : null}
      <section className="results-section results-chain">
        <header className="panel-title results-head">
          <span>Flow</span>
        </header>
        <FactoryGraph plan={plan} unit={unit} />
      </section>
    </>
  )
}
