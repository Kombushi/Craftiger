import { useState } from 'react'
import { planStatusNotes } from '../factoryContext'
import { fmtEuT, fmtRate } from '../format'
import { usePlanner } from '../plannerContext'
import { nodeId, snap, tidyPositions } from '../plannerGrid'
import { useStore } from '../storeContext'
import type { PlannerNode, PlannerStep, RateUnit } from '../types'
import { usePersistent } from '../usePersistent'
import { AddNodeMenu, type AddNodeChoice, type AddNodeItem } from './AddNodeMenu'
import { CanvasMenu } from './CanvasMenu'
import { FactoryWarnings } from './FactoryWarnings'
import { GaragePanel } from './GaragePanel'
import { GeneratorPickerModal } from './GeneratorPickerModal'
import { ItemSearchModal } from './ItemSearchModal'
import { PlannerCanvas } from './PlannerCanvas'
import { PlannerPalette } from './PlannerPalette'
import { RecipePickerModal } from './RecipePickerModal'
import { SidebarLayout } from './SidebarLayout'

type Picker =
  | { kind: 'create'; screen: { x: number; y: number }; grid: { x: number; y: number } }
  | { kind: 'item'; choice: AddNodeChoice; grid: { x: number; y: number } }
  | { kind: 'menu'; item: AddNodeItem; allow: AddNodeChoice[] }
  | { kind: 'recipe'; itemId: string; position: { x: number; y: number } | null }
  | { kind: 'generator' }
  | null

const searchTitles: Record<AddNodeChoice, string> = {
  input: 'Place an Input — what do you have on hand?',
  output: 'Place an Output — what must the pipeline make?',
  step: 'Add a producing step — what should it make?',
}

/** The manual pipeline grid: right-click places nodes, the balance is live, ghosts click into new nodes. */
export function PlannerPage({ sidebarHidden }: { sidebarHidden: boolean }) {
  const planner = usePlanner()
  const { garage, meta } = useStore()
  const [unit, setUnit] = usePersistent<RateUnit>('gtnhp.rateUnit', 'second')
  const [picker, setPicker] = useState<Picker>(null)
  const { plan, status, nodes } = planner
  const hasEnergy = nodes.some((node) => node.kind === 'energy')
  const stepNames = Object.fromEntries(
    nodes.flatMap((node) => (node.kind === 'step' ? [[node.id, node.label]] : [])),
  )
  const anchored = nodes.some(
    (node) => node.kind === 'output' || node.kind === 'energy' || (node.kind === 'input' && node.amount !== null),
  )
  const feeding = nodes.some((node) => node.kind === 'step' || (node.kind === 'input' && node.amount === null))

  /** A loose free spot in the given column, under whatever already sits there. */
  const spotIn = (x: number): { x: number; y: number } => ({
    x,
    y: snap(40 + nodes.filter((node) => Math.abs(node.x - x) < 240).length * 130),
  })

  /** Where a producer of the item belongs: left of the step consuming it, or the step column. */
  const producerSpot = (itemId: string): { x: number; y: number } => {
    const consumers = nodes.filter(
      (node) =>
        node.kind === 'step' &&
        plan?.lines.some(
          (line) => line.recipeId === node.id && (line.inputs ?? []).some((flow) => flow.itemId === itemId),
        ),
    )
    if (consumers.length === 0) {
      return spotIn(420)
    }
    const first = consumers.reduce((best, node) => (node.x < best.x ? node : best))
    return { x: snap(Math.max(0, first.x - 460)), y: snap(first.y) }
  }

  const addEnergy = (position: { x: number; y: number }) => {
    const tier = Math.max(1, garage.defaultTier)
    planner.addNode({ kind: 'energy', amps: 1, tier, euT: meta?.tierVoltages[tier] ?? 32, ...position })
    setPicker(null)
  }

  const place = (choice: AddNodeChoice, item: AddNodeItem, position: { x: number; y: number } | null) => {
    if (choice === 'step') {
      setPicker({ kind: 'recipe', itemId: item.itemId, position })
      return
    }
    const node: PlannerNode =
      choice === 'input'
        ? {
            kind: 'input', itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx,
            amount: null, window: 1, windowUnit: 'second', ...(position ?? spotIn(0)),
          }
        : {
            kind: 'output', itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx,
            amount: 1, window: 1, windowUnit: 'second', ...(position ?? spotIn(980)),
          }
    planner.addNode(node)
    setPicker(null)
  }

  const addStep = (step: PlannerStep, position: { x: number; y: number } | null) => {
    planner.addNode({ ...step, kind: 'step', ...(position ?? spotIn(420)) })
    setPicker(null)
  }

  const ghostIn = (itemId: string) => {
    const item = plan?.items[itemId]
    setPicker({
      kind: 'menu',
      item: { itemId, name: item?.name ?? itemId, atlasIdx: item?.atlasIdx ?? -1 },
      allow: ['input', 'step'],
    })
  }

  const ghostOut = (itemId: string) => {
    const item = plan?.items[itemId]
    setPicker({
      kind: 'menu',
      item: { itemId, name: item?.name ?? itemId, atlasIdx: item?.atlasIdx ?? -1 },
      allow: ['output'],
    })
  }

  const tidy = () => {
    const positions = tidyPositions(nodes, plan)
    planner.setNodes(nodes.map((node) => ({ ...node, ...(positions.get(nodeId(node)) ?? {}) })))
  }

  const hint =
    nodes.length === 0
      ? 'An empty grid — right-click the canvas to place a node.'
      : !anchored
        ? 'Nothing anchors the balance yet — place an Output, an Energy node, or rate an Input.'
        : !feeding
          ? 'Nothing feeds the pipeline yet — place a step or an Input.'
          : status.phase === 'solving'
            ? plan === null
              ? 'Balancing the pipeline…'
              : 'Re-balancing…'
            : null

  return (
    <>
      <SidebarLayout
        hidden={sidebarHidden}
        sidebar={
          <>
            <PlannerPalette onPickGenerator={() => setPicker({ kind: 'generator' })} />
            <GaragePanel
              targetIds={nodes.flatMap((node) =>
                node.kind === 'output' || (node.kind === 'input' && node.amount !== null) ? [node.itemId] : [],
              )}
            />
          </>
        }
      >
        <section className="results-section">
          <div className="grid-toolbar">
            {plan !== null && plan.status === 'solved' ? (
              <div className="factory-totals mono">
                <span title="Purchased inflows at their standing prices">₴ {fmtRate(plan.pricedInflowCost, unit)}</span>
                <span title="Total machine draw">{fmtEuT(plan.drawEuT)} draw</span>
                {plan.exportEuT > 0 ? (
                  <span title="Generator emission minus the plan's own draw">
                    +{fmtEuT(plan.exportEuT - plan.drawEuT)} net
                  </span>
                ) : null}
                <span title={`${plan.busyMachines.toFixed(2)} continuously busy`}>
                  {plan.lines.reduce((sum, line) => sum + Math.ceil(line.busyMachines), 0)}× machines
                </span>
              </div>
            ) : (
              <span />
            )}
            <span className="grid-toolbar-right">
              {hint !== null ? <span className="hint">{hint}</span> : null}
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
              <button
                type="button"
                className="ghost-button"
                title="Re-run the layered auto-layout over the grid"
                disabled={nodes.length === 0}
                onClick={tidy}
              >
                Tidy
              </button>
            </span>
          </div>
          {plan !== null ? <FactoryWarnings plan={plan} names={stepNames} /> : null}
          {plan !== null && plan.status !== 'solved' ? (
            <p className="hint">{planStatusNotes[plan.status] ?? plan.status}</p>
          ) : null}
        </section>
        <section className="results-section results-chain">
          <PlannerCanvas
            unit={unit}
            onGhostIn={ghostIn}
            onGhostOut={ghostOut}
            onCreate={(screen, grid) => setPicker({ kind: 'create', screen, grid })}
          />
        </section>
      </SidebarLayout>
      {picker?.kind === 'create' ? (
        <CanvasMenu
          at={picker.screen}
          hasEnergy={hasEnergy}
          onPick={(choice) =>
            choice === 'energy'
              ? addEnergy(picker.grid)
              : setPicker({ kind: 'item', choice, grid: picker.grid })
          }
          onClose={() => setPicker(null)}
        />
      ) : null}
      {picker?.kind === 'item' ? (
        <ItemSearchModal
          title={searchTitles[picker.choice]}
          onPick={(item) =>
            place(picker.choice, { itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx }, picker.grid)
          }
          onClose={() => setPicker(null)}
        />
      ) : null}
      {picker?.kind === 'menu' ? (
        <AddNodeMenu
          item={picker.item}
          allow={picker.allow}
          onPick={(choice) =>
            place(
              choice,
              picker.item,
              choice !== 'output' ? producerSpot(picker.item.itemId) : null,
            )
          }
          onClose={() => setPicker(null)}
        />
      ) : null}
      {picker?.kind === 'recipe' ? (
        <RecipePickerModal
          itemId={picker.itemId}
          onPick={(step) => addStep(step, picker.position)}
          onClose={() => setPicker(null)}
        />
      ) : null}
      {picker?.kind === 'generator' ? (
        <GeneratorPickerModal
          onPick={(step) => addStep(step, null)}
          onClose={() => setPicker(null)}
        />
      ) : null}
    </>
  )
}
