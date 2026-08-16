import { ItemIcon } from './ItemIcon'

interface Props {
  atlasIdx: number
  badge?: string
  title?: string
  size?: 'md' | 'sm'
  highlight?: boolean
  dim?: boolean
  onClick?: () => void
  onHover?: (hovering: boolean) => void
}

/** The recessed beveled item slot every view is built from. */
export function Slot({ atlasIdx, badge, title, size = 'md', highlight, dim, onClick, onHover }: Props) {
  const classes = [
    'slot',
    size === 'sm' ? 'slot-sm' : '',
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
      <ItemIcon atlasIdx={atlasIdx} size={size === 'sm' ? 24 : 32} />
      {badge ? <span className="slot-badge">{badge}</span> : null}
    </span>
  )
}