import { useState } from 'react'
import { planStatusNotes } from '../factoryContext'
import { usePlanner } from '../plannerContext'
import type { RateUnit } from '../types'
import { usePersistent } from '../usePersistent'
import { FactoryWarnings } from './FactoryWarnings'
import { GaragePanel } from './GaragePanel'
import { GeneratorPickerModal } from './GeneratorPickerModal'
import { PlanResults } from './PlanResults'
import { PlannerStepsPanel } from './PlannerStepsPanel'
import { RecipePickerModal } from './RecipePickerModal'
import { SidebarLayout } from './SidebarLayout'

type Picker = { kind: 'recipe'; itemId: string } | { kind: 'generator' } | null

/** The manual pipeline tab: hand-picked steps, live re-solve, and inflows that click into new steps. */
export function PlannerPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const planner = usePlanner()
  const [unit, setUnit] = usePersistent<RateUnit>('gtnhp.rateUnit', 'second')
  const [picker, setPicker] = useState<Picker>(null)
  const { plan, status, steps, targets } = planner
  const stepNames = Object.fromEntries(steps.map((step) => [step.id, step.label]))

  return (
    <>
      <SidebarLayout
        hidden={sidebarHidden}
        sidebar={
          <>
            <PlannerStepsPanel
              onPickRecipe={(itemId) => setPicker({ kind: 'recipe', itemId })}
              onPickGenerator={() => setPicker({ kind: 'generator' })}
            />
            <GaragePanel
              targetIds={targets.flatMap((target) => (target.kind === 'energy' ? [] : [target.itemId]))}
            />
          </>
        }
      >
        {plan === null ? (
          <div className="results-empty">
            <p className="hint">
              {status.phase === 'solving'
                ? 'Balancing the pipeline…'
                : steps.length === 0
                  ? 'Add steps on the left — the pipeline runs exactly what you pick.'
                  : 'Add a target to anchor the pipeline: a rate to produce or absorb, or an energy row.'}
            </p>
          </div>
        ) : (
          <>
            {status.phase === 'solving' ? <p className="stale-banner">Re-balancing…</p> : null}
            <FactoryWarnings plan={plan} names={stepNames} />
            {plan.status !== 'solved' ? (
              <div className="results-empty">
                <p className="hint">{planStatusNotes[plan.status] ?? plan.status}</p>
              </div>
            ) : (
              <PlanResults
                plan={plan}
                unit={unit}
                setUnit={setUnit}
                onInflowClick={(itemId) => setPicker({ kind: 'recipe', itemId })}
              />
            )}
          </>
        )}
      </SidebarLayout>
      {picker?.kind === 'recipe' ? (
        <RecipePickerModal
          itemId={picker.itemId}
          onPick={(step) => {
            planner.addStep(step)
            setPicker(null)
          }}
          onClose={() => setPicker(null)}
        />
      ) : null}
      {picker?.kind === 'generator' ? (
        <GeneratorPickerModal
          onPick={(step) => {
            planner.addStep(step)
            setPicker(null)
          }}
          onClose={() => setPicker(null)}
        />
      ) : null}
    </>
  )
}
