import { useRef, type CSSProperties, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react'
import { usePersistent } from '../usePersistent'

const SIDEBAR_MIN = 280
const SIDEBAR_MAX = 640
const SIDEBAR_DEFAULT = 380

const clampSidebar = (width: number) => Math.min(SIDEBAR_MAX, Math.max(SIDEBAR_MIN, width))

/** The two-column tab shell: a resizable sidebar, its drag handle, and the results main. */
export function SidebarLayout({
  hidden,
  sidebar,
  children,
}: {
  hidden: boolean
  sidebar: ReactNode
  children: ReactNode
}) {
  const plannerRef = useRef<HTMLDivElement | null>(null)
  const [sidebarWidth, setSidebarWidth] = usePersistent('gtnhp.sidebarWidth', SIDEBAR_DEFAULT)

  // The drag writes the CSS variable directly so the results are not re-rendered
  // per pointer move; React state catches up once on release.
  const dragSidebar = (event: ReactPointerEvent<HTMLDivElement>) => {
    const planner = plannerRef.current
    if (planner === null) {
      return
    }
    event.preventDefault()
    const handle = event.currentTarget
    handle.setPointerCapture(event.pointerId)
    const left = planner.getBoundingClientRect().left
    document.body.style.cursor = 'col-resize'
    let width = sidebarWidth
    const move = (moveEvent: PointerEvent) => {
      width = clampSidebar(Math.round(moveEvent.clientX - left))
      planner.style.setProperty('--sidebar-width', `${width}px`)
    }
    const stop = () => {
      handle.removeEventListener('pointermove', move)
      handle.removeEventListener('pointerup', stop)
      handle.removeEventListener('pointercancel', stop)
      document.body.style.cursor = ''
      setSidebarWidth(width)
    }
    handle.addEventListener('pointermove', move)
    handle.addEventListener('pointerup', stop)
    handle.addEventListener('pointercancel', stop)
  }

  return (
    <div
      ref={plannerRef}
      className={`planner${hidden ? ' planner-collapsed' : ''}`}
      style={{ '--sidebar-width': `${sidebarWidth}px` } as CSSProperties}
    >
      <aside className="sidebar">{sidebar}</aside>
      <div
        className="sidebar-handle"
        title="Drag to resize; double-click to reset"
        onPointerDown={dragSidebar}
        onDoubleClick={() => {
          plannerRef.current?.style.removeProperty('--sidebar-width')
          setSidebarWidth(SIDEBAR_DEFAULT)
        }}
      />
      <main className="results">{children}</main>
    </div>
  )
}
