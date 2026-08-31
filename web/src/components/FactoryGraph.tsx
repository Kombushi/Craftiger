import { useMemo, useState } from 'react'
import { FOOTER, HEADER, PAD, SLOT, edgePath, slotGridWidth, type ChainOrientation } from '../chainLayout'
import { estimateNote, layoutFactory, type FactoryCard } from '../factoryLayout'
import { fmtAka, fmtCost, fmtDuration, fmtEuT, fmtRate, fmtRateBadge } from '../format'
import { useStore } from '../storeContext'
import { useTooltipTarget } from '../tooltipContext'
import type { FactoryResponse, RateUnit } from '../types'
import { usePersistent } from '../usePersistent'
import { GraphViewport } from './GraphViewport'
import { Slot } from './Slot'

export function FactoryGraph({ plan, unit }: { plan: FactoryResponse; unit: RateUnit }) {
  const [orientation, setOrientation] = usePersistent<ChainOrientation>(
    'gtnhp.chainOrientation',
    'horizontal',
  )
  const layout = useMemo(() => layoutFactory(plan, orientation), [plan, orientation])
  const [hovered, setHovered] = useState<string | null>(null)

  return (
    <GraphViewport
      width={layout.width}
      height={layout.height}
      orientation={orientation}
      onToggleOrientation={() => setOrientation(orientation === 'vertical' ? 'horizontal' : 'vertical')}
    >
      <svg
        className="chain-edges"
        width={layout.width}
        height={layout.height}
        viewBox={`0 0 ${Math.max(1, layout.width)} ${Math.max(1, layout.height)}`}
      >
        {layout.edges.map((edge, index) => (
          <path
            key={index}
            className={`edge${edge.loop ? ' edge-loop' : ''}${hovered === edge.itemId ? ' edge-active' : ''}`}
            d={edgePath(edge, orientation)}
          />
        ))}
      </svg>
      {layout.cards.map((card) =>
        card.kind === 'line' ? (
          <LineCard key={card.id} card={card} plan={plan} unit={unit} onHover={setHovered} />
        ) : (
          <SourceCard key={card.id} card={card} plan={plan} unit={unit} onHover={setHovered} />
        ),
      )}
    </GraphViewport>
  )
}

interface CardProps {
  card: FactoryCard
  plan: FactoryResponse
  unit: RateUnit
  onHover: (itemId: string | null) => void
}

function LineCard({ card, plan, unit, onHover }: CardProps) {
  const { openDetail } = useStore()
  const line = card.line!
  const machineName =
    (line.machineItemId !== null ? plan.items[line.machineItemId]?.name : undefined) ?? line.machine
  const est = estimateNote(line)
  const bodyTop = HEADER + PAD
  const countTip = useTooltipTarget({
    name: `${line.busyMachines.toFixed(2)} busy machines`,
    lines: [
      `${fmtRate(line.runsPerSecond, unit)} runs`,
      ...(line.parallels > 1 ? [`×${line.parallels} parallels per machine`] : []),
      ...(line.ocSteps > 0 ? [`overclocked ${line.ocSteps} step${line.ocSteps === 1 ? '' : 's'}`] : []),
    ],
  })
  const estTip = useTooltipTarget(est === null ? undefined : { name: 'EST', lines: [est] })

  const flowSlot = (flow: { itemId: string; perSecond: number }, key: string, output: boolean) => {
    const item = plan.items[flow.itemId]
    const isFluid = item?.isFluid ?? false
    return (
      <Slot
        key={key}
        atlasIdx={item?.atlasIdx ?? -1}
        badge={fmtRateBadge(flow.perSecond, unit)}
        highlight={output}
        tooltip={{
          name: fmtAka(item, flow.itemId),
          lines: [`${fmtRate(flow.perSecond, unit, isFluid)} ${output ? 'produced' : 'consumed'}`],
        }}
        onClick={() => openDetail(flow.itemId)}
        onHover={(hovering) => onHover(hovering ? flow.itemId : null)}
      />
    )
  }

  return (
    <div
      className="card card-recipe"
      style={{ left: card.x, top: card.y, width: card.w, height: card.h }}
    >
      <header className="card-head">
        <span className="card-machine" title={line.recipeId}>
          {machineName}
        </span>
        {card.loop ? (
          <span className="tag tag-loop" title="Feeds itself: these lines consume each other's output">
            LOOP
          </span>
        ) : null}
        {est !== null ? (
          <span
            className="tag tag-est"
            onPointerEnter={estTip.onPointerEnter}
            onPointerMove={estTip.onPointerMove}
            onPointerLeave={estTip.onPointerLeave}
          >
            EST
          </span>
        ) : null}
        {line.ocSteps > 0 ? <span className="tag tag-chip mono">OC×{line.ocSteps}</span> : null}
        {line.parallels > 1 ? <span className="tag tag-chip mono">P×{line.parallels}</span> : null}
        <span
          className="card-runs mono"
          onPointerEnter={countTip.onPointerEnter}
          onPointerMove={countTip.onPointerMove}
          onPointerLeave={countTip.onPointerLeave}
        >
          {Math.ceil(line.busyMachines)}×
        </span>
      </header>
      <div
        className="card-grid"
        style={{
          left: PAD,
          top: bodyTop,
          width: slotGridWidth(card.inCols),
          gridTemplateColumns: `repeat(${card.inCols}, ${SLOT}px)`,
        }}
      >
        {(line.inputs ?? []).map((flow, slot) => flowSlot(flow, `in-${slot}`, false))}
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
        {(line.outputs ?? []).map((flow, slot) => flowSlot(flow, `out-${slot}`, true))}
      </div>
      <footer className="card-foot mono" style={{ height: FOOTER }}>
        <span>
          {line.durationless
            ? 'instant · free'
            : line.euTPerMachine < 0
              ? `+${fmtEuT(-line.euTPerMachine)} per machine`
              : `${fmtDuration(line.durationSeconds * 20)} · ${fmtEuT(line.euTPerMachine)}`}
        </span>
      </footer>
    </div>
  )
}

/** A stream entering from outside: a purchased or auto-infinite inflow, or a consume target's intake. */
function SourceCard({ card, plan, unit, onHover }: CardProps) {
  const { openDetail } = useStore()
  const itemId = card.itemId!
  const item = plan.items[itemId]
  const isFluid = item?.isFluid ?? false
  const inflow = plan.inflows.find((flow) => flow.itemId === itemId)
  const supplied = plan.flows.find((flow) => flow.itemId === itemId)?.supplied ?? 0
  const rate = card.kind === 'intake' ? supplied : (inflow?.rate ?? 0)
  const free = inflow?.autoInfinite ?? false
  const sub =
    card.kind === 'intake'
      ? `${fmtRate(rate, unit, isFluid)} intake`
      : `${fmtRate(rate, unit, isFluid)} · ${free ? '∞ free' : `${fmtCost(inflow?.weight ?? null)} each`}`
  const tip = useTooltipTarget({ name: fmtAka(item, itemId), lines: [sub] })
  return (
    <div
      className="card card-leaf"
      style={{ left: card.x, top: card.y, width: card.w, height: card.h }}
      onMouseEnter={() => onHover(itemId)}
      onMouseLeave={() => onHover(null)}
      onPointerEnter={tip.onPointerEnter}
      onPointerMove={tip.onPointerMove}
      onPointerLeave={tip.onPointerLeave}
    >
      <Slot
        atlasIdx={item?.atlasIdx ?? -1}
        badge={fmtRateBadge(rate, unit)}
        needBadge={free ? '∞' : undefined}
        onClick={() => {
          tip.hide()
          openDetail(itemId)
        }}
      />
      <span className="leaf-text">
        <span className="leaf-name">{item?.name ?? itemId}</span>
        <span className="leaf-sub mono">{sub}</span>
      </span>
    </div>
  )
}
