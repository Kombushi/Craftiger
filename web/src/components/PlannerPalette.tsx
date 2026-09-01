import { useFactory } from '../factoryContext'
import { usePlanner } from '../plannerContext'
import { useStore } from '../storeContext'
import type { PlannerNode } from '../types'
import type { AddNodeItem } from './AddNodeMenu'
import { SearchBox } from './SearchBox'

interface Props {
  onPlace: (item: AddNodeItem) => void
  onPickGenerator: () => void
}

/** The Planner sidebar: one search to place nodes, the energy and generator adders, and the Factory import. */
export function PlannerPalette({ onPlace, onPickGenerator }: Props) {
  const planner = usePlanner()
  const factory = useFactory()
  const { garage, meta } = useStore()
  const { nodes } = planner
  const hasEnergy = nodes.some((node) => node.kind === 'energy')
  const hasSteps = nodes.some((node) => node.kind === 'step')

  const addEnergy = () => {
    const tier = Math.max(1, garage.defaultTier)
    const euT = meta?.tierVoltages[tier] ?? 32
    planner.addNode({ kind: 'energy', amps: 1, tier, euT, x: 980, y: 40 })
  }

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
      <SearchBox
        placeholder="Search an item to place…"
        onPick={(item) => onPlace({ itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx })}
      />
      {nodes.length === 0 ? (
        <p className="hint">
          Everything lives on the grid: place an Output to anchor the pipeline, Inputs for what you
          have on hand, and steps for the machines between them.
        </p>
      ) : (
        <p className="hint">
          {nodes.length} node{nodes.length === 1 ? '' : 's'} on the grid — click a dashed ghost to
          grow the pipeline.
        </p>
      )}
      <div className="step-actions">
        <button
          type="button"
          className="ghost-button target-add-energy"
          title={hasEnergy ? 'The grid already has its energy node' : 'Place the energy export node'}
          disabled={hasEnergy || meta === null}
          onClick={addEnergy}
        >
          + energy node
        </button>
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
