import { useMemo, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react'
import { FOOTER, HEADER, PAD, SLOT, edgePath, slotGridWidth } from '../chainLayout'
import { estimateNote } from '../factoryLayout'
import { fmtAka, fmtCost, fmtDuration, fmtEuT, fmtRate, fmtRateBadge } from '../format'
import { usePlanner } from '../plannerContext'
import { STEP_CONTROLS, buildGrid, nodeId, snap, type GridCard } from '../plannerGrid'
import { useStore } from '../storeContext'
import type {
  EnergyNode,
  FactoryResponse,
  InputNode,
  OutputNode,
  RateUnit,
  StepNode,
} from '../types'
import { GraphViewport } from './GraphViewport'
import { Slot } from './Slot'
import { Stepper } from './Stepper'

interface Props {
  unit: RateUnit
  /** A ghost inflow was clicked — offer to adopt it as an Input or add its producer. */
  onGhostIn: (itemId: string) => void
  /** A ghost surplus was clicked — offer to adopt it as an Output. */
  onGhostOut: (itemId: string) => void
}

/** The Planner grid canvas: user-placed node cards, solver-drawn edges, ghosts on the frontier. */
export function PlannerCanvas({ unit, onGhostIn, onGhostOut }: Props) {
  const planner = usePlanner()
  const { nodes, plan } = planner
  const grid = useMemo(() => buildGrid(nodes, plan), [nodes, plan])
  const [hovered, setHovered] = useState<string | null>(null)

  return (
    <GraphViewport width={grid.width} height={grid.height}>
      <svg
        className="chain-edges"
        width={grid.width}
        height={grid.height}
        viewBox={`0 0 ${Math.max(1, grid.width)} ${Math.max(1, grid.height)}`}
      >
        {grid.edges.map((edge, index) => (
          <path
            key={index}
            className={`edge${hovered === edge.itemId ? ' edge-active' : ''}`}
            d={edgePath(edge, 'horizontal')}
          />
        ))}
      </svg>
      {grid.cards.map((card) => {
        switch (card.kind) {
          case 'line':
            return <StepCard key={card.id} card={card} plan={plan} unit={unit} onHover={setHovered} />
          case 'input':
            return <InputCard key={card.id} card={card} plan={plan} unit={unit} />
          case 'output':
            return <OutputCard key={card.id} card={card} plan={plan} unit={unit} />
          case 'energy':
            return <EnergyCard key={card.id} card={card} plan={plan} />
          default:
            return (
              <GhostCard
                key={card.id}
                card={card}
                plan={plan}
                unit={unit}
                onHover={setHovered}
                onClick={card.kind === 'ghost-in' ? onGhostIn : onGhostOut}
              />
            )
        }
      })}
    </GraphViewport>
  )
}

/** The zoom the canvas renders at, read off its transform so drags track the pointer 1:1. */
function canvasScale(element: HTMLElement): number {
  const canvas = element.closest('.chain-canvas')
  if (!(canvas instanceof HTMLElement)) {
    return 1
  }
  const match = /matrix\(([^,]+),/.exec(getComputedStyle(canvas).transform)
  return match ? Number(match[1]) : 1
}

/** Positions a node card and lets it drag; interactive children opt out, a real drag snaps home on release. */
function NodeFrame({ card, className, children }: { card: GridCard; className: string; children: ReactNode }) {
  const planner = usePlanner()
  const [delta, setDelta] = useState<{ dx: number; dy: number } | null>(null)
  const drag = useRef<{ pointerId: number; startX: number; startY: number; k: number; moved: boolean } | null>(null)
  const node = card.node!

  return (
    <div
      className={className}
      style={{
        left: card.x + (delta?.dx ?? 0),
        top: card.y + (delta?.dy ?? 0),
        width: card.w,
        height: card.h,
      }}
      onPointerDown={(event: ReactPointerEvent<HTMLDivElement>) => {
        if (event.button !== 0 || (event.target as HTMLElement).closest('button, input, select')) {
          return
        }
        event.stopPropagation()
        drag.current = {
          pointerId: event.pointerId,
          startX: event.clientX,
          startY: event.clientY,
          k: canvasScale(event.currentTarget),
          moved: false,
        }
      }}
      onPointerMove={(event) => {
        const state = drag.current
        if (!state || state.pointerId !== event.pointerId) {
          return
        }
        const dx = (event.clientX - state.startX) / state.k
        const dy = (event.clientY - state.startY) / state.k
        // Capture only once a real drag starts — capturing on pointerdown swallows child clicks.
        if (!state.moved && Math.abs(dx) + Math.abs(dy) > 3) {
          state.moved = true
          event.currentTarget.setPointerCapture(event.pointerId)
        }
        if (state.moved) {
          setDelta({ dx, dy })
        }
      }}
      onPointerUp={(event) => {
        const state = drag.current
        drag.current = null
        setDelta(null)
        if (state?.moved) {
          const dx = (event.clientX - state.startX) / state.k
          const dy = (event.clientY - state.startY) / state.k
          planner.moveNode(nodeId(node), snap(node.x + dx), snap(node.y + dy))
        }
      }}
      onPointerCancel={() => {
        drag.current = null
        setDelta(null)
      }}
      onClickCapture={(event) => {
        if (delta !== null) {
          event.stopPropagation()
        }
      }}
    >
      {children}
    </div>
  )
}

function RemoveButton({ id, what }: { id: string; what: string }) {
  const planner = usePlanner()
  return (
    <button
      type="button"
      className="ghost-button"
      title={`Remove this ${what}`}
      onClick={() => planner.removeNode(id)}
    >
      ×
    </button>
  )
}

interface CardProps {
  card: GridCard
  plan: FactoryResponse | null
  unit: RateUnit
}

function StepCard({ card, plan, unit, onHover }: CardProps & { onHover: (itemId: string | null) => void }) {
  const planner = usePlanner()
  const { openDetail } = useStore()
  const node = card.node as StepNode
  const line = card.line
  const generator = node.id.startsWith('generator|')
  const locked = node.machineItemId !== null || node.ocSteps !== null
  const shownOc = node.ocSteps ?? line?.ocSteps ?? 0
  const machineName =
    (line?.machineItemId != null ? plan?.items[line.machineItemId]?.name : undefined) ??
    (node.machineItemId != null ? plan?.items[node.machineItemId]?.name : undefined) ??
    node.machine
  const est = line !== null ? estimateNote(line) : null
  const bodyTop = HEADER + PAD
  const update = (next: Partial<StepNode>) => planner.updateNode(nodeId(node), { ...node, ...next })

  const flowSlot = (flow: { itemId: string; perSecond: number }, key: string, output: boolean) => {
    const item = plan?.items[flow.itemId]
    return (
      <Slot
        key={key}
        atlasIdx={item?.atlasIdx ?? -1}
        badge={fmtRateBadge(flow.perSecond, unit)}
        highlight={output}
        tooltip={{
          name: fmtAka(item, flow.itemId),
          lines: [`${fmtRate(flow.perSecond, unit, item?.isFluid ?? false)} ${output ? 'produced' : 'consumed'}`],
        }}
        onClick={() => openDetail(flow.itemId)}
        onHover={(hovering) => onHover(hovering ? flow.itemId : null)}
      />
    )
  }

  return (
    <NodeFrame card={card} className="card card-recipe card-node">
      <header className="card-head">
        <span className="card-machine" title={`${node.label} — ${node.id}`}>
          {machineName}
        </span>
        {est !== null ? (
          <span className="tag tag-est" title={est}>
            EST
          </span>
        ) : null}
        {line !== null && line.parallels > 1 ? <span className="tag tag-chip mono">P×{line.parallels}</span> : null}
        <span className="card-runs mono" title={line !== null ? `${line.busyMachines.toFixed(2)} busy machines` : 'idle'}>
          {line !== null ? `${Math.ceil(line.busyMachines)}×` : '0×'}
        </span>
      </header>
      {line !== null ? (
        <>
          <div
            className="card-grid"
            style={{
              left: PAD,
              top: bodyTop,
              width: slotGridWidth(card.inCols),
              gridTemplateColumns: `repeat(${card.inCols}, ${SLOT}px)`,
            }}
          >
            {line.inputs.map((flow, slot) => flowSlot(flow, `in-${slot}`, false))}
          </div>
          <span
            className="card-arrow"
            style={{
              left: PAD + slotGridWidth(card.inCols),
              top: bodyTop,
              width: card.w - PAD * 2 - slotGridWidth(card.inCols) - slotGridWidth(card.outCols),
            }}
          >
            ▶
          </span>
          <div
            className="card-grid"
            style={{
              left: card.w - PAD - slotGridWidth(card.outCols),
              top: bodyTop,
              width: slotGridWidth(card.outCols),
              gridTemplateColumns: `repeat(${card.outCols}, ${SLOT}px)`,
            }}
          >
            {line.outputs.map((flow, slot) => flowSlot(flow, `out-${slot}`, true))}
          </div>
        </>
      ) : (
        <div className="card-idle" style={{ top: bodyTop }}>
          <Slot atlasIdx={node.atlasIdx} size="sm" tooltip={{ name: node.label }} />
          <span className="leaf-sub">
            {plan?.status === 'solved' ? 'idle — nothing routes through this step' : 'waiting for a balance…'}
          </span>
        </div>
      )}
      <div className="step-controls" style={{ bottom: FOOTER, height: STEP_CONTROLS }}>
        {!generator ? (
          <>
            <button
              type="button"
              className={`pin-button${locked ? ' pin-active' : ''}`}
              title={
                locked
                  ? 'Locked to this block and overclock — click to free the choice'
                  : 'Lock the chosen block and overclock'
              }
              onClick={() =>
                update(
                  locked
                    ? { machineItemId: null, ocSteps: null }
                    : { machineItemId: line?.machineItemId ?? null, ocSteps: line?.ocSteps ?? 0 },
                )
              }
            >
              {locked ? 'LOCKED' : 'LOCK'}
            </button>
            <span className="step-oc mono">
              <button type="button" tabIndex={-1} title="One overclock step down" onClick={() => update({ ocSteps: Math.max(0, shownOc - 1) })}>
                −
              </button>
              OC {shownOc}
              <button type="button" tabIndex={-1} title="One overclock step up" onClick={() => update({ ocSteps: shownOc + 1 })}>
                +
              </button>
            </span>
          </>
        ) : null}
        <span className="step-spacer" />
        <RemoveButton id={nodeId(node)} what="step" />
      </div>
      <footer className="card-foot mono" style={{ height: FOOTER }}>
        <span>
          {line === null
            ? node.label
            : line.durationless
              ? 'instant · free'
              : line.euTPerMachine < 0
                ? `+${fmtEuT(-line.euTPerMachine)} per machine`
                : `${fmtDuration(line.durationSeconds * 20)} · ${fmtEuT(line.euTPerMachine)}`}
        </span>
      </footer>
    </NodeFrame>
  )
}

/** The rate editor an Input shares with an Output: amount / window unit. */
function RateEditor({
  amount,
  window,
  windowUnit,
  onChange,
}: {
  amount: number
  window: number
  windowUnit: RateUnit
  onChange: (next: { amount: number; window: number; windowUnit: RateUnit }) => void
}) {
  return (
    <span className="target-entry io-rate">
      <Stepper
        className="target-amount"
        min={1}
        value={amount}
        onChange={(next) => onChange({ amount: Math.floor(next), window, windowUnit })}
      />
      <span className="target-per mono">/</span>
      <Stepper
        className="target-window"
        min={1}
        value={window}
        onChange={(next) => onChange({ amount, window: Math.floor(next), windowUnit })}
      />
      <select
        className="target-unit"
        value={windowUnit}
        onChange={(event) => onChange({ amount, window, windowUnit: event.target.value as RateUnit })}
      >
        <option value="tick">t</option>
        <option value="second">s</option>
        <option value="minute">min</option>
      </select>
    </span>
  )
}

function InputCard({ card, plan, unit }: CardProps) {
  const planner = usePlanner()
  const { openDetail } = useStore()
  const node = card.node as InputNode
  const unbounded = node.amount === null
  const rate = unbounded
    ? (plan?.inflows.find((inflow) => inflow.itemId === node.itemId)?.rate ?? 0)
    : (plan?.flows.find((flow) => flow.itemId === node.itemId)?.supplied ?? 0)
  const update = (next: Partial<InputNode>) => planner.updateNode(nodeId(node), { ...node, ...next })

  return (
    <NodeFrame card={card} className="card card-io card-node">
      <header className="card-head">
        <span className="io-kind io-kind-in">IN</span>
        <span className="card-machine" title={node.name}>
          {node.name}
        </span>
        <RemoveButton id={nodeId(node)} what="input" />
      </header>
      <div className="io-body">
        <Slot
          atlasIdx={node.atlasIdx}
          size="sm"
          badge={rate > 0 ? fmtRateBadge(rate, unit) : undefined}
          needBadge={unbounded ? '∞' : undefined}
          tooltip={{
            name: node.name,
            lines: [unbounded ? 'free unbounded source' : 'fixed intake the pipeline must absorb'],
          }}
          onClick={() => openDetail(node.itemId)}
        />
        {unbounded ? (
          <button
            type="button"
            className="ghost-button io-mode"
            title="Unbounded free source — click to fix the intake rate instead"
            onClick={() => update({ amount: 1, window: 1, windowUnit: 'second' })}
          >
            ∞ free
          </button>
        ) : (
          <>
            <RateEditor
              amount={node.amount!}
              window={node.window}
              windowUnit={node.windowUnit}
              onChange={(next) => update(next)}
            />
            <button
              type="button"
              className="ghost-button io-mode"
              title="Fixed intake — click to make it an unbounded free source"
              onClick={() => update({ amount: null })}
            >
              ∞
            </button>
          </>
        )}
      </div>
    </NodeFrame>
  )
}

function OutputCard({ card, plan, unit }: CardProps) {
  const planner = usePlanner()
  const { openDetail } = useStore()
  const node = card.node as OutputNode
  const produced = plan?.flows.find((flow) => flow.itemId === node.itemId)?.produced ?? 0
  const update = (next: Partial<OutputNode>) => planner.updateNode(nodeId(node), { ...node, ...next })

  return (
    <NodeFrame card={card} className="card card-io card-node">
      <header className="card-head">
        <span className="io-kind io-kind-out">OUT</span>
        <span className="card-machine" title={node.name}>
          {node.name}
        </span>
        <RemoveButton id={nodeId(node)} what="output" />
      </header>
      <div className="io-body">
        <Slot
          atlasIdx={node.atlasIdx}
          size="sm"
          badge={produced > 0 ? fmtRateBadge(produced, unit) : undefined}
          highlight
          tooltip={{ name: node.name, lines: ['the rate this pipeline must produce'] }}
          onClick={() => openDetail(node.itemId)}
        />
        <RateEditor
          amount={node.amount}
          window={node.window}
          windowUnit={node.windowUnit}
          onChange={(next) => update(next)}
        />
      </div>
    </NodeFrame>
  )
}

function EnergyCard({ card, plan }: { card: GridCard; plan: FactoryResponse | null }) {
  const planner = usePlanner()
  const { meta } = useStore()
  const node = card.node as EnergyNode
  const voltages = meta?.tierVoltages ?? []
  const tierNames = meta?.tierNames ?? []
  const net = plan !== null && plan.status === 'solved' && plan.exportEuT > 0 ? plan.exportEuT - plan.drawEuT : null
  const update = (next: Partial<EnergyNode>) => planner.updateNode('energy', { ...node, ...next })

  return (
    <NodeFrame card={card} className="card card-io card-energy card-node">
      <header className="card-head">
        <span className="io-kind io-kind-energy">⚡</span>
        <span className="card-machine">Energy export</span>
        {net !== null ? <span className="card-runs mono">+{fmtEuT(net)} net</span> : null}
        <RemoveButton id="energy" what="energy node" />
      </header>
      <div className="io-body">
        <span className="target-entry io-rate">
          <Stepper
            className="target-amount"
            min={1}
            value={node.amps}
            onChange={(amps) => {
              const whole = Math.floor(amps)
              update({ amps: whole, euT: whole * (voltages[node.tier] ?? 0) })
            }}
          />
          <span className="target-per mono">A ×</span>
          <select
            className="target-unit"
            title="The tier the exported power arrives at; generators below it do not count"
            value={node.tier}
            onChange={(event) => {
              const tier = Number(event.target.value)
              update({ tier, euT: node.amps * (voltages[tier] ?? 0) })
            }}
          >
            {tierNames.map((name, tier) =>
              tier > 0 ? (
                <option key={name} value={tier}>
                  {name}
                </option>
              ) : null,
            )}
          </select>
          <span className="target-rate mono">
            ={' '}
            <Stepper
              className="target-eut"
              min={1}
              value={node.euT}
              onChange={(euT) => update({ euT: Math.floor(euT) })}
            />{' '}
            EU/t
          </span>
        </span>
      </div>
    </NodeFrame>
  )
}

function GhostCard({
  card,
  plan,
  unit,
  onHover,
  onClick,
}: CardProps & { onHover: (itemId: string | null) => void; onClick: (itemId: string) => void }) {
  const itemId = card.itemId!
  const item = plan?.items[itemId]
  const entering = card.kind === 'ghost-in'
  const inflow = plan?.inflows.find((flow) => flow.itemId === itemId)
  const surplus = plan?.flows.find((flow) => flow.itemId === itemId)?.surplus ?? 0
  const sub = entering
    ? `${fmtRate(inflow?.rate ?? 0, unit, item?.isFluid ?? false)} · ${
        inflow?.autoInfinite ? '∞ free' : `${fmtCost(inflow?.weight ?? null)} each`
      }`
    : `+${fmtRate(surplus, unit, item?.isFluid ?? false)} spare`

  return (
    <div
      className="card card-leaf card-ghost"
      style={{ left: card.x, top: card.y, width: card.w, height: card.h }}
      title={entering ? 'Supplied from outside — click to adopt it' : 'Left over — click to make it an output'}
      onMouseEnter={() => onHover(itemId)}
      onMouseLeave={() => onHover(null)}
      onClick={() => onClick(itemId)}
    >
      <Slot
        atlasIdx={item?.atlasIdx ?? -1}
        badge={fmtRateBadge(entering ? (inflow?.rate ?? 0) : surplus, unit)}
        needBadge={entering && inflow?.autoInfinite ? '∞' : undefined}
      />
      <span className="leaf-text">
        <span className="leaf-name">{item?.name ?? itemId}</span>
        <span className="leaf-sub mono">{sub}</span>
      </span>
    </div>
  )
}
