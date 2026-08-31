import { useFactory } from '../factoryContext'
import { usePlanner } from '../plannerContext'
import type { FactoryLine, FactoryResponse, PlannerStep } from '../types'
import { PrioritySelect } from './FactoryTargetsPanel'
import { SearchBox } from './SearchBox'
import { Slot } from './Slot'
import { TargetsEditor } from './TargetsEditor'

interface Props {
  onPickRecipe: (itemId: string) => void
  onPickGenerator: () => void
}

export function PlannerStepsPanel({ onPickRecipe, onPickGenerator }: Props) {
  const planner = usePlanner()
  const factory = useFactory()
  const hasEnergy = planner.targets.some((target) => target.kind === 'energy')

  const importFromFactory = () => {
    const plan = factory.plan
    if (plan === null) {
      return
    }
    const steps: PlannerStep[] = []
    for (const line of plan.lines) {
      if (line.machine === 'Cleanroom' || steps.some((step) => step.id === line.recipeId)) {
        continue
      }
      const product = line.outputs?.[0]
      const item = product !== undefined ? plan.items[product.itemId] : undefined
      steps.push({
        id: line.recipeId,
        label: item?.name ?? line.machine,
        atlasIdx: item?.atlasIdx ?? -1,
        machine: line.machine,
        machineItemId: null,
        ocSteps: null,
      })
    }
    planner.setSteps(steps)
  }

  return (
    <section className="panel">
      <header className="panel-title">Pipeline steps</header>
      <SearchBox
        placeholder="Search an item to add its recipe…"
        onPick={(item) => onPickRecipe(item.itemId)}
      />
      {planner.steps.length === 0 ? (
        <p className="hint">
          Search an item and pick which recipe makes it — the pipeline runs only the steps you add.
        </p>
      ) : (
        <ul className="cart">
          {planner.steps.map((step, index) => (
            <StepRow key={step.id} step={step} index={index} plan={planner.plan} />
          ))}
        </ul>
      )}
      <div className="step-actions">
        <button
          type="button"
          className="ghost-button target-add-energy"
          title={
            hasEnergy
              ? 'Pick a generator line to serve the energy target'
              : 'Add an energy target first — generator steps feed it'
          }
          disabled={!hasEnergy}
          onClick={onPickGenerator}
        >
          + generator step
        </button>
        {planner.steps.length === 0 && factory.plan?.status === 'solved' ? (
          <button
            type="button"
            className="ghost-button target-add-energy"
            title="Copy the Factory tab's solved lines as editable steps"
            onClick={importFromFactory}
          >
            start from the Factory plan
          </button>
        ) : null}
      </div>
      <header className="panel-title">Targets</header>
      <SearchBox
        placeholder="Search an item to target…"
        onPick={(item) =>
          planner.addItemTarget({ itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx })
        }
      />
      {planner.targets.length === 0 ? (
        <p className="hint">Anchor the pipeline: a produce or consume rate, or an energy target.</p>
      ) : null}
      <TargetsEditor store={planner} />
      <PrioritySelect priority={planner.priority} setPriority={planner.setPriority} />
      <label className="garage-toggle" title="Admit mob-drop seeds as free inputs">
        <input
          type="checkbox"
          checked={planner.mobFarms}
          onChange={(event) => planner.setMobFarms(event.target.checked)}
        />
        <span>Mob farms</span>
      </label>
      <label className="garage-toggle" title="Rate crop-farm steps at bred 31/31 seeds">
        <input
          type="checkbox"
          checked={planner.bredSeeds}
          onChange={(event) => planner.setBredSeeds(event.target.checked)}
        />
        <span>Bred seeds</span>
      </label>
    </section>
  )
}

/** The step's plan line with the most machines — what its lock captures. */
function chosenLine(plan: FactoryResponse | null, stepId: string): FactoryLine | null {
  const lines = (plan?.lines ?? []).filter((line) => line.recipeId === stepId)
  if (lines.length === 0) {
    return null
  }
  return lines.reduce((best, line) => (line.busyMachines >= best.busyMachines ? line : best))
}

function StepRow({ step, index, plan }: { step: PlannerStep; index: number; plan: FactoryResponse | null }) {
  const planner = usePlanner()
  const generator = step.id.startsWith('generator|')
  const chosen = chosenLine(plan, step.id)
  const locked = step.machineItemId !== null || step.ocSteps !== null
  const shownOc = step.ocSteps ?? chosen?.ocSteps ?? 0
  const blockName =
    (chosen?.machineItemId != null ? plan?.items[chosen.machineItemId]?.name : undefined) ??
    step.machine
  const nudge = (delta: number) =>
    planner.updateStep(index, { ...step, ocSteps: Math.max(0, shownOc + delta) })

  return (
    <li className="cart-row target-row">
      <Slot atlasIdx={step.atlasIdx} size="sm" tooltip={{ name: step.label }} />
      <span className="cart-name" title={`${step.label} — ${step.id}`}>
        {step.label}
      </span>
      <button
        type="button"
        className="ghost-button"
        title="Remove this step"
        onClick={() => planner.removeStep(index)}
      >
        ×
      </button>
      <span className="target-entry">
        <span className="step-machine mono" title={blockName}>
          {blockName}
        </span>
        {!generator && (chosen !== null || locked) ? (
          <span className="step-oc mono">
            <button type="button" tabIndex={-1} title="One overclock step down" onClick={() => nudge(-1)}>
              −
            </button>
            OC {shownOc}
            <button type="button" tabIndex={-1} title="One overclock step up" onClick={() => nudge(1)}>
              +
            </button>
          </span>
        ) : null}
        {!generator ? (
          <button
            type="button"
            className={`pin-button${locked ? ' pin-active' : ''}`}
            title={
              locked
                ? 'Locked to this block and overclock — click to free the choice'
                : 'Lock the chosen block and overclock'
            }
            onClick={() =>
              planner.updateStep(
                index,
                locked
                  ? { ...step, machineItemId: null, ocSteps: null }
                  : {
                      ...step,
                      machineItemId: chosen?.machineItemId ?? null,
                      ocSteps: chosen?.ocSteps ?? 0,
                    },
              )
            }
          >
            {locked ? 'LOCKED' : 'LOCK'}
          </button>
        ) : null}
        {chosen !== null ? (
          <span className="target-rate mono">×{Math.ceil(chosen.busyMachines)}</span>
        ) : null}
      </span>
    </li>
  )
}
