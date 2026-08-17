import { useEffect, useMemo, useRef, useState } from 'react'
import { FOOTER, HEADER, PAD, SLOT, SLOT_GAP, layoutChain, type ChainCard } from '../chainLayout'
import { fmtAmount, fmtCost, fmtCount, fmtDuration, fmtHeat, fmtRuns } from '../format'
import { useStore } from '../storeContext'
import type { BomResponse } from '../types'
import { Slot } from './Slot'

const MARGIN = 40

interface View {
  x: number
  y: number
  k: number
}

export function ChainGraph({ bom }: { bom: BomResponse }) {
  const layout = useMemo(() => layoutChain(bom), [bom])
  const viewport = useRef<HTMLDivElement>(null)
  const [view, setView] = useState<View>({ x: MARGIN, y: MARGIN, k: 1 })
  const [hovered, setHovered] = useState<string | null>(null)
  const drag = useRef<{ pointerId: number; startX: number; startY: number; moved: boolean } | null>(
    null,
  )

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
          startX: event.clientX,
          startY: event.clientY,
          moved: false,
        }
        event.currentTarget.setPointerCapture(event.pointerId)
      }}
      onPointerMove={(event) => {
        const state = drag.current
        if (!state || state.pointerId !== event.pointerId) {
          return
        }
        const dx = event.clientX - state.startX
        const dy = event.clientY - state.startY
        if (Math.abs(dx) + Math.abs(dy) > 3) {
          state.moved = true
        }
        state.startX = event.clientX
        state.startY = event.clientY
        setView((previous) => ({ ...previous, x: previous.x + dx, y: previous.y + dy }))
      }}
      onPointerUp={() => {
        drag.current = null
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
          {layout.edges.map((edge, index) => {
            const bend = Math.min(60, (edge.x2 - edge.x1) / 2)
            const active = hovered === edge.itemId
            return (
              <path
                key={index}
                className={active ? 'edge edge-active' : 'edge'}
                d={`M ${edge.x1} ${edge.y1} C ${edge.x1 + bend} ${edge.y1}, ${edge.x2 - bend} ${edge.y2}, ${edge.x2} ${edge.y2}`}
              />
            )
          })}
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

function RecipeCard({ card, bom, onHover }: CardProps) {
  const { meta, pins, openDetail } = useStore()
  const node = card.node!
  const tierName = meta?.tierNames[node.tier] ?? String(node.tier)
  const pinned = pins[node.itemId] === node.recipeId
  const bodyTop = HEADER + PAD
  const gridWidth = (columns: number) => columns * SLOT + (columns - 1) * SLOT_GAP

  return (
    <div
      className={`card card-recipe${pinned ? ' card-pinned' : ''}`}
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
        {node.inputsPerRun.map((input, index) => {
          const item = bom.items[input.itemId]
          const isFluid = item?.isFluid ?? false
          const total = input.amount * node.wholeRuns
          return (
            <Slot
              key={index}
              atlasIdx={item?.atlasIdx ?? -1}
              badge={fmtCount(total)}
              title={`${item?.name ?? input.itemId}\n${fmtAmount(input.amount, isFluid)} per run · ${fmtAmount(total, isFluid)} total (${fmtCount(input.amount * node.runs)} expected)`}
              onClick={() => openDetail(input.itemId)}
              onHover={(hovering) => onHover(hovering ? input.itemId : null)}
            />
          )
        })}
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
          const produced = output.amount * node.wholeRuns
          const chance = output.chance < 1 ? ` · ${Math.round(output.chance * 100)}%` : ''
          const spare = own && output.chance === 1 ? produced - node.wholeAmount : 0
          const need = own
            ? `\nneed ${fmtCount(node.wholeAmount)}${spare > 0 ? `, +${fmtCount(spare)} spare` : ''}`
            : '\nbyproduct — not credited'
          return (
            <Slot
              key={index}
              atlasIdx={item?.atlasIdx ?? -1}
              badge={fmtCount(produced)}
              dim={!own}
              highlight={own}
              title={`${item?.name ?? output.itemId}${chance}${need}`}
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
        <button
          type="button"
          className={`pin-button${pinned ? ' pin-active' : ''}`}
          title={pinned ? 'Pinned — open recipes' : 'Choose a recipe'}
          onClick={() => openDetail(node.itemId)}
        >
          {pinned ? 'PINNED' : 'PIN'}
        </button>
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
        <span className="leaf-name" title={item?.name ?? card.id}>
          {item?.name ?? card.id}
        </span>
        <span className="leaf-sub mono">
          {missing
            ? 'unreachable'
            : `${fmtAmount(card.amount, item?.isFluid ?? false)} · ${fmtCost(item?.cost ?? null)} each`}
        </span>
      </span>
    </div>
  )
}