import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { ChainOrientation } from '../chainLayout'

const MARGIN = 40

interface View {
  x: number
  y: number
  k: number
}

interface Props {
  width: number
  height: number
  /** The layout direction the toggle button flips; the Planner grid has none and omits both. */
  orientation?: ChainOrientation
  onToggleOrientation?: () => void
  children: ReactNode
}

/** The pan-zoom-fit canvas the flow graphs and the Planner grid render into; children are the edges and cards at layout coordinates. */
export function GraphViewport({ width, height, orientation, onToggleOrientation, children }: Props) {
  const viewport = useRef<HTMLDivElement>(null)
  const [view, setView] = useState<View>({ x: MARGIN, y: MARGIN, k: 1 })
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
    if (!element || width === 0) {
      return
    }
    const bounds = element.getBoundingClientRect()
    const k = Math.min(1, (bounds.width - MARGIN * 2) / width, (bounds.height - MARGIN * 2) / height)
    setView({
      x: (bounds.width - width * k) / 2,
      y: (bounds.height - height * k) / 2,
      k: Math.max(0.15, k),
    })
  }

  // Refit whenever a different graph arrives.
  useEffect(fit, [width, height])

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
        {children}
      </div>
      <div className="chain-controls">
        {onToggleOrientation !== undefined ? (
          <button
            type="button"
            className="ghost-button"
            title={orientation === 'vertical' ? 'Horizontal layout' : 'Vertical layout'}
            onClick={onToggleOrientation}
          >
            {orientation === 'vertical' ? '⇄' : '⇅'}
          </button>
        ) : null}
        <button type="button" className="ghost-button" title="Fit to view" onClick={fit}>
          ⤢
        </button>
      </div>
    </div>
  )
}
