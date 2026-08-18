import { ItemIcon } from './ItemIcon'

const ICON_SIZE = { sm: 24, md: 32, lg: 48 } as const

interface Props {
  atlasIdx: number
  badge?: string
  needBadge?: string
  title?: string
  size?: 'md' | 'sm' | 'lg'
  highlight?: boolean
  dim?: boolean
  onClick?: () => void
  onHover?: (hovering: boolean) => void
}

/** The recessed beveled item slot every view is built from. */
export function Slot({
  atlasIdx, badge, needBadge, title, size = 'md', highlight, dim, onClick, onHover,
}: Props) {
  const classes = [
    'slot',
    size === 'sm' ? 'slot-sm' : '',
    size === 'lg' ? 'slot-lg' : '',
    highlight ? 'slot-highlight' : '',
    dim ? 'slot-dim' : '',
    onClick ? 'slot-clickable' : '',
  ]
    .filter(Boolean)
    .join(' ')
  return (
    <span
      className={classes}
      title={title}
      onClick={onClick}
      onMouseEnter={onHover ? () => onHover(true) : undefined}
      onMouseLeave={onHover ? () => onHover(false) : undefined}
    >
      <ItemIcon atlasIdx={atlasIdx} size={ICON_SIZE[size]} />
      {badge ? <span className="slot-badge">{badge}</span> : null}
      {needBadge ? <span className="slot-need">{needBadge}</span> : null}
    </span>
  )
}
