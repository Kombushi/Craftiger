import type { ItemSummary } from '../types'
import { SearchBox } from './SearchBox'

/** Names the item a freshly placed node is about. */
export function ItemSearchModal({
  title,
  onPick,
  onClose,
}: {
  title: string
  onPick: (item: ItemSummary) => void
  onClose: () => void
}) {
  return (
    <div className="overlay" onClick={onClose}>
      <div className="modal" onClick={(event) => event.stopPropagation()}>
        <header className="modal-head">
          <span className="panel-title">{title}</span>
          <button type="button" className="ghost-button" onClick={onClose}>
            ×
          </button>
        </header>
        <SearchBox placeholder="Search an item…" autoFocus onPick={onPick} />
      </div>
    </div>
  )
}
