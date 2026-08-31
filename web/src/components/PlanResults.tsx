import { estimateNote } from '../factoryLayout'
import { fmtAka, fmtCost, fmtEuT, fmtRate, fmtRateBadge } from '../format'
import { useStore } from '../storeContext'
import type { FactoryResponse, RateUnit } from '../types'
import { FactoryGraph } from './FactoryGraph'
import { Slot } from './Slot'

interface Props {
  plan: FactoryResponse
  unit: RateUnit
  setUnit: (unit: RateUnit) => void
  /** Where an inflow slot leads; the Planner opens its add-step picker, the default is item detail. */
  onInflowClick?: (itemId: string) => void
}

/** The solved-plan sections both rate tabs share: totals, entering streams, surplus, and the flow graph. */
export function PlanResults({ plan, unit, setUnit, onInflowClick }: Props) {
  const { openDetail } = useStore()
  const inflowClick = onInflowClick ?? openDetail
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
                  onClick={() => inflowClick(inflow.itemId)}
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
