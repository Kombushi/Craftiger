import { useEffect, useMemo, useRef, useState } from 'react'
import {
  FOOTER,
  HEADER,
  PAD,
  SLOT,
  SLOT_GAP,
  edgePath,
  gridExtras,
  layoutChain,
  type ChainCard,
  type ChainOrientation,
} from '../chainLayout'
import { fmtAka, fmtAmount, fmtCost, fmtCount, fmtDuration, fmtHeat, fmtRuns } from '../format'
import { useStore } from '../storeContext'
import type { BomNode, BomResponse } from '../types'
import { usePersistent } from '../usePersistent'
import { Slot } from './Slot'

const MARGIN = 40

interface View {
  x: number
  y: number
  k: number
}

export function ChainGraph({ bom }: { bom: BomResponse }) {
  const [orientation, setOrientation] = usePersistent<ChainOrientation>(
    'gtnhp.chainOrientation',
    'horizontal',
  )
  const layout = useMemo(() => layoutChain(bom, orientation), [bom, orientation])
  const viewport = useRef<HTMLDivElement>(null)
  const [view, setView] = useState<View>({ x: MARGIN, y: MARGIN, k: 1 })
  const [hovered, setHovered] = useState<string | null>(null)
  const drag = useRef<{
    pointerId: number
    originX: number
    originY: number
    lastX: number
    lastY: number
    moved: boolean
  } | null>(null)

  const fit = () => {
    const element = viewport.current
    if (!element || layout.width === 0) {
      return
    }
    const bounds = element.getBoundingClientRect()
    const k = Math.min(
      1,
      (bounds.width - MARGIN * 2) / layout.width,
      (bounds.height - MARGIN * 2) / layout.height,
    )
    setView({
      x: (bounds.width - layout.width * k) / 2,
      y: (bounds.height - layout.height * k) / 2,
      k: Math.max(0.15, k),
    })
  }

  // Refit whenever a different chain arrives.
  useEffect(fit, [layout])

  useEffect(() => {
    const element = viewport.current
    if (!element) {
      return
    }
    const onWheel = (event: WheelEvent) => {
      event.preventDefault()
      const bounds = element.getBoundingClientRect()
      const cx = event.clientX - bounds.left
      const cy = event.clientY - bounds.top
      setView((previous) => {
        const k = Math.min(2.5, Math.max(0.1, previous.k * Math.exp(-event.deltaY * 0.0012)))
        return {
          k,
          x: cx - ((cx - previous.x) * k) / previous.k,
          y: cy - ((cy - previous.y) * k) / previous.k,
        }
      })
    }
    element.addEventListener('wheel', onWheel, { passive: false })
    return () => element.removeEventListener('wheel', onWheel)
  }, [])

  return (
    <div
      ref={viewport}
      className="chain-viewport"
      onPointerDown={(event) => {
        if (event.button !== 0) {
          return
        }
        drag.current = {
          pointerId: event.pointerId,
          originX: event.clientX,
          originY: event.clientY,
          lastX: event.clientX,
          lastY: event.clientY,
          moved: false,
        }
      }}
      onPointerMove={(event) => {
        const state = drag.current
        if (!state || state.pointerId !== event.pointerId) {
          return
        }
        const dx = event.clientX - state.lastX
        const dy = event.clientY - state.lastY
        state.lastX = event.clientX
        state.lastY = event.clientY
        // Capture only once a real drag starts — capturing on pointerdown swallows child clicks.
        if (
          !state.moved &&
          Math.abs(event.clientX - state.originX) + Math.abs(event.clientY - state.originY) > 4
        ) {
          state.moved = true
          event.currentTarget.setPointerCapture(event.pointerId)
        }
        setView((previous) => ({ ...previous, x: previous.x + dx, y: previous.y + dy }))
      }}
      onPointerUp={() => {
        drag.current = null
      }}
      onPointerLeave={() => {
        if (drag.current !== null && !drag.current.moved) {
          drag.current = null
        }
      }}
      onClickCapture={(event) => {
        if (drag.current?.moved) {
          event.stopPropagation()
        }
      }}
    >
      <div
        className="chain-canvas"
        style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.k})` }}
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
          card.kind === 'recipe' ? (
            <RecipeCard key={card.id} card={card} bom={bom} onHover={setHovered} />
          ) : (
            <EndCard key={card.id} card={card} bom={bom} onHover={setHovered} />
          ),
        )}
      </div>
      <div className="chain-controls">
        <button
          type="button"
          className="ghost-button"
          title={orientation === 'vertical' ? 'Horizontal layout' : 'Vertical layout'}
          onClick={() => setOrientation(orientation === 'vertical' ? 'horizontal' : 'vertical')}
        >
          {orientation === 'vertical' ? '⇄' : '⇅'}
        </button>
        <button type="button" className="ghost-button" title="Fit to view" onClick={fit}>
          ⤢
        </button>
      </div>
    </div>
  )
}

interface CardProps {
  card: ChainCard
  bom: BomResponse
  onHover: (itemId: string | null) => void
}

/** Every input slot in card order: the consumed inputs, then the catalysts. */
function allSlots(node: BomNode): number[] {
  return Array.from({ length: node.inputsPerRun.length + node.catalysts.length }, (_, slot) => slot)
}

function RecipeCard({ card, bom, onHover }: CardProps) {
  const { meta, openDetail } = useStore()
  const node = card.node!
  const tierName = meta?.tierNames[node.tier] ?? String(node.tier)
  const bodyTop = HEADER + PAD
  const gridWidth = (columns: number) => columns * SLOT + (columns - 1) * SLOT_GAP
  const inputCount = node.inputsPerRun.length
  // A shaped recipe's cell holds one item per craft: the slot's folded amount spreads over its cells.
  const cellsOf = (slot: number) =>
    node.grid === null ? 1 : Math.max(1, node.grid.filter((held) => held === slot).length)
  const inputSlot = (slot: number, key: string, as: 'slot' | 'cell') => {
    if (slot < inputCount) {
      const input = node.inputsPerRun[slot]
      const item = bom.items[input.itemId]
      const isFluid = item?.isFluid ?? false
      const perRun = as === 'cell' ? input.amount / cellsOf(slot) : input.amount
      const total = perRun * node.wholeRuns
      return (
        <Slot
          key={key}
          atlasIdx={item?.atlasIdx ?? -1}
          badge={as === 'cell' ? undefined : fmtCount(total)}
          title={`${fmtAka(item, input.itemId)}\n${fmtAmount(perRun, isFluid)} per run · ${fmtAmount(total, isFluid)} total (${fmtCount(perRun * node.runs)} expected)`}
          onClick={() => openDetail(input.itemId)}
          onHover={(hovering) => onHover(hovering ? input.itemId : null)}
        />
      )
    }
    const tool = node.catalysts[slot - inputCount]
    const item = bom.items[tool.itemId]
    return (
      <Slot
        key={key}
        atlasIdx={item?.atlasIdx ?? -1}
        badge={as === 'cell' ? undefined : fmtCount(tool.amount)}
        dim
        title={`${fmtAka(item, tool.itemId)}\nneeded in place — not consumed`}
        onClick={() => openDetail(tool.itemId)}
        onHover={(hovering) => onHover(hovering ? tool.itemId : null)}
      />
    )
  }

  return (
    <div
      className="card card-recipe"
      style={{ left: card.x, top: card.y, width: card.w, height: card.h }}
    >
      <header className="card-head">
        <span className="card-machine" title={node.machine}>
          {node.machine}
        </span>
        <span className="card-tier mono">
          {tierName}
          {node.heat !== null ? ` · ${fmtHeat(node.heat)}` : ''}
        </span>
        {node.seed ? (
          <span className="tag tag-seed" title="The one outside unit that starts the loop">
            SEED
          </span>
        ) : node.loop !== null ? (
          <span className="tag tag-loop" title="Feeds itself: these recipes consume each other's output">
            LOOP
          </span>
        ) : null}
        <span
          className="card-runs mono"
          title={`${node.wholeRuns} whole run${node.wholeRuns === 1 ? '' : 's'} (${fmtCount(node.runs)} expected)`}
        >
          {fmtRuns(node.wholeRuns)}
        </span>
      </header>
      <div
        className={`card-grid${node.machine === 'Crafting Table' ? ' card-grid-bench' : ''}`}
        style={{
          left: PAD,
          top: bodyTop,
          width: gridWidth(card.inCols),
          gridTemplateColumns: `repeat(${card.inCols}, ${SLOT}px)`,
        }}
      >
        {node.grid === null
          ? allSlots(node).map((slot) => inputSlot(slot, String(slot), 'slot'))
          : [
              ...node.grid.map((slot, cell) =>
                slot === null ? <span key={`cell-${cell}`} className="slot slot-empty" /> : inputSlot(slot, `cell-${cell}`, 'cell'),
              ),
              ...gridExtras(node).map((slot) => inputSlot(slot, `extra-${slot}`, 'slot')),
            ]}
      </div>
      <span
        className="card-arrow"
        style={{
          left: PAD + gridWidth(card.inCols),
          top: bodyTop,
          width: card.w - PAD * 2 - gridWidth(card.inCols) - gridWidth(card.outCols),
        }}
      >
        ▶
      </span>
      <div
        className="card-grid"
        style={{
          left: card.w - PAD - gridWidth(card.outCols),
          top: bodyTop,
          width: gridWidth(card.outCols),
          gridTemplateColumns: `repeat(${card.outCols}, ${SLOT}px)`,
        }}
      >
        {node.outputs.map((output, index) => {
          const item = bom.items[output.itemId]
          const own = output.itemId === node.itemId
          // Chanced recipes list the same item several times; the need shows once.
          const firstOwn = index === node.outputs.findIndex((o) => o.itemId === node.itemId)
          const produced = output.amount * node.wholeRuns
          const chance = output.chance < 1 ? ` · ${Math.round(output.chance * 100)}%` : ''
          return (
            <Slot
              key={index}
              atlasIdx={item?.atlasIdx ?? -1}
              badge={fmtCount(produced)}
              needBadge={own && firstOwn ? fmtCount(node.wholeAmount) : undefined}
              dim={!own}
              highlight={own}
              title={`${fmtAka(item, output.itemId)}${chance}${own ? '' : '\nbyproduct — not credited'}`}
              onClick={() => openDetail(output.itemId)}
              onHover={(hovering) => onHover(hovering ? output.itemId : null)}
            />
          )
        })}
      </div>
      <footer className="card-foot mono" style={{ height: FOOTER }}>
        <span>
          {fmtDuration(node.durationTicks)} · {node.euT.toLocaleString('en-US')} EU/t
        </span>
      </footer>
    </div>
  )
}

function EndCard({ card, bom, onHover }: CardProps) {
  const { openDetail } = useStore()
  const item = bom.items[card.id]
  const missing = card.kind === 'missing'
  const expected = bom.leaves.find((leaf) => leaf.itemId === card.id)?.amount
  return (
    <div
      className={`card card-leaf${missing ? ' card-missing' : ''}`}
      style={{ left: card.x, top: card.y, width: card.w, height: card.h }}
      title={expected === undefined ? undefined : `${fmtCount(expected)} expected`}
      onMouseEnter={() => onHover(card.id)}
      onMouseLeave={() => onHover(null)}
    >
      <Slot
        atlasIdx={item?.atlasIdx ?? -1}
        badge={missing ? undefined : fmtCount(card.amount)}
        onClick={() => openDetail(card.id)}
      />
      <span className="leaf-text">
        <span className="leaf-name" title={fmtAka(item, card.id)}>
          {item?.name ?? card.id}
        </span>
        <span className="leaf-sub mono">
          {missing
            ? item?.uncraftable
              ? 'uncraftable'
              : 'unreachable'
            : `${fmtAmount(card.amount, item?.isFluid ?? false)} · ${fmtCost(item?.cost ?? null)} each`}
        </span>
      </span>
    </div>
  )
}