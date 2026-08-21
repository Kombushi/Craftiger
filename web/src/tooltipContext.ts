import { createContext, useContext, useEffect, useRef, type PointerEvent as ReactPointerEvent } from 'react'

/** What a slot's tooltip says: the item on the first line, its figures under it. */
export interface TooltipContent {
  name: string
  lines?: string[]
}

export interface TooltipApi {
  show(content: TooltipContent, x: number, y: number): void
  move(x: number, y: number): void
  hide(): void
}

export const TooltipContext = createContext<TooltipApi | null>(null)

/** Pointer handlers that drive the shared tooltip for one element; a mouse only, so touch
 * never pins a tooltip under a finger, and the tooltip goes with the element when it unmounts. */
export function useTooltipTarget(content: TooltipContent | undefined): {
  onPointerEnter: (event: ReactPointerEvent) => void
  onPointerMove: (event: ReactPointerEvent) => void
  onPointerLeave: () => void
  hide: () => void
} {
  const api = useContext(TooltipContext)
  const hovering = useRef(false)
  useEffect(
    () => () => {
      if (hovering.current) {
        api?.hide()
      }
    },
    [api],
  )
  const hide = () => {
    hovering.current = false
    api?.hide()
  }
  return {
    onPointerEnter: (event) => {
      if (content && api && event.pointerType === 'mouse') {
        hovering.current = true
        api.show(content, event.clientX, event.clientY)
      }
    },
    onPointerMove: (event) => {
      if (hovering.current && api && event.pointerType === 'mouse') {
        api.move(event.clientX, event.clientY)
      }
    },
    onPointerLeave: hide,
    hide,
  }
}
