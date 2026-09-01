import { useFactory } from '../factoryContext'
import { usePlanner } from '../plannerContext'
import type { PlannerNode } from '../types'

interface Props {
  onPickGenerator: () => void
}

/** The Planner sidebar: the grid grows by right-click; only the generator adder and the Factory import live here. */
export function PlannerPalette({ onPickGenerator }: Props) {
  const planner = usePlanner()
  const factory = useFactory()
  const { nodes } = planner
  const hasEnergy = nodes.some((node) => node.kind === 'energy')
  const hasSteps = nodes.some((node) => node.kind === 'step')

  const importFromFactory = () => {
    const plan = factory.plan
    if (plan === null) {
      return
    }
    const steps: PlannerNode[] = []
    for (const line of plan.lines) {
      if (line.machine === 'Cleanroom' || steps.some((step) => step.kind === 'step' && step.id === line.recipeId)) {
        continue
      }
      const product = line.outputs?.[0]
      const item = product !== undefined ? plan.items[product.itemId] : undefined
      steps.push({
        kind: 'step',
        id: line.recipeId,
        label: item?.name ?? line.machine,
        atlasIdx: item?.atlasIdx ?? -1,
        machine: line.machine,
        machineItemId: null,
        ocSteps: null,
        scope: null,
        x: 420,
        y: 40 + steps.length * 170,
      })
    }
    planner.setNodes([...planner.nodes, ...steps])
  }

  return (
    <section className="panel">
      <header className="panel-title">Grid</header>
      {nodes.length === 0 ? (
        <p className="hint">
          Right-click the canvas to place a node: an Output anchors the pipeline, Inputs are what
          you have on hand, and producing steps are the machines between them.
        </p>
      ) : (
        <p className="hint">
          {nodes.length} node{nodes.length === 1 ? '' : 's'} on the grid — right-click to add more,
          click a dashed ghost to grow the pipeline.
        </p>
      )}
      <div className="step-actions">
        <button
          type="button"
          className="ghost-button target-add-energy"
          title={
            hasEnergy
              ? 'Pick a generator line to feed the energy node'
              : 'Place the energy node first — generator steps feed it'
          }
          disabled={!hasEnergy}
          onClick={onPickGenerator}
        >
          + generator step
        </button>
        {!hasSteps && factory.plan?.status === 'solved' ? (
          <button
            type="button"
            className="ghost-button target-add-energy"
            title="Copy the Factory tab's solved lines onto the grid as steps"
            onClick={importFromFactory}
          >
            start from the Factory plan
          </button>
        ) : null}
      </div>
    </section>
  )
}
