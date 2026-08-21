import { useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { TooltipContext, type TooltipApi, type TooltipContent } from '../tooltipContext'

/** NEI's offset: the panel hangs to the lower right of the cursor. */
const OFFSET = 12
const EDGE = 4

/** One tooltip panel for the whole app, following the cursor and flipping to stay inside the
 * window; the targets feed it through the context. */
export function TooltipProvider({ children }: { children: ReactNode }) {
  const [content, setContent] = useState<TooltipContent | null>(null)
  const layer = useRef<HTMLDivElement>(null)
  const point = useRef({ x: 0, y: 0 })

  const place = () => {
    const element = layer.current
    if (!element) {
      return
    }
    const { x, y } = point.current
    const width = element.offsetWidth
    const height = element.offsetHeight
    let left = x + OFFSET
    if (left + width > window.innerWidth - EDGE) {
      left = Math.max(EDGE, x - OFFSET - width)
    }
    let top = y - OFFSET
    if (top + height > window.innerHeight - EDGE) {
      top = Math.max(EDGE, window.innerHeight - EDGE - height)
    }
    element.style.transform = `translate(${left}px, ${top}px)`
  }

  // The panel's size is only known once its content is in the DOM.
  useLayoutEffect(place, [content])

  const api = useMemo<TooltipApi>(
    () => ({
      show: (next, x, y) => {
        point.current = { x, y }
        setContent(next)
      },
      move: (x, y) => {
        point.current = { x, y }
        place()
      },
      hide: () => setContent(null),
    }),
    [],
  )

  return (
    <TooltipContext.Provider value={api}>
      {children}
      {content ? (
        <div ref={layer} className="tooltip" role="tooltip">
          <div className="tooltip-name">{content.name}</div>
          {(content.lines ?? []).map((line, index) => (
            <div key={index} className="tooltip-line mono">
              {line}
            </div>
          ))}
        </div>
      ) : null}
    </TooltipContext.Provider>
  )
}
