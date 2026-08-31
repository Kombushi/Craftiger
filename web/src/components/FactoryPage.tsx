import { planStatusNotes, useFactory } from '../factoryContext'
import type { RateUnit } from '../types'
import { usePersistent } from '../usePersistent'
import { FactoryTargetsPanel } from './FactoryTargetsPanel'
import { FactoryWarnings } from './FactoryWarnings'
import { GaragePanel } from './GaragePanel'
import { PlanResults } from './PlanResults'
import { SidebarLayout } from './SidebarLayout'

export function FactoryPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const { targets, plan, status, stale, solve } = useFactory()
  const [unit, setUnit] = usePersistent<RateUnit>('gtnhp.rateUnit', 'second')

  return (
    <SidebarLayout
      hidden={sidebarHidden}
      sidebar={
        <>
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
        </>
      }
    >
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
          {stale ? <p className="stale-banner">Settings changed — showing the last plan.</p> : null}
          <FactoryWarnings plan={plan} />
          {plan.status !== 'solved' ? (
            <div className="results-empty">
              <p className="hint">{planStatusNotes[plan.status] ?? plan.status}</p>
            </div>
          ) : (
            <PlanResults plan={plan} unit={unit} setUnit={setUnit} />
          )}
        </>
      )}
    </SidebarLayout>
  )
}
