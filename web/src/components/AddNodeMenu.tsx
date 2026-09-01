import { Slot } from './Slot'

export interface AddNodeItem {
  itemId: string
  name: string
  atlasIdx: number
}

export type AddNodeChoice = 'output' | 'input' | 'step'

const labels: Record<AddNodeChoice, { title: string; hint: string }> = {
  output: { title: 'Output node', hint: 'The pipeline must produce it at a rate you set' },
  input: { title: 'Input node', hint: 'A free source you have on hand — unbounded until you rate it' },
  step: { title: 'Producing step…', hint: 'Pick which recipe or farm makes it' },
}

/** Chooses what a picked item becomes on the grid. */
export function AddNodeMenu({
  item,
  allow,
  onPick,
  onClose,
}: {
  item: AddNodeItem
  allow: AddNodeChoice[]
  onPick: (choice: AddNodeChoice) => void
  onClose: () => void
}) {
  return (
    <div className="overlay" onClick={onClose}>
      <div className="modal" onClick={(event) => event.stopPropagation()}>
        <header className="modal-head">
          <span className="panel-title">
            <Slot atlasIdx={item.atlasIdx} size="sm" /> Place {item.name}
          </span>
          <button type="button" className="ghost-button" onClick={onClose}>
            ×
          </button>
        </header>
        <ul className="picker-list">
          {allow.map((choice) => (
            <li key={choice}>
              <button type="button" className="picker-row" onClick={() => onPick(choice)}>
                <span className="picker-main">
                  <span className="picker-title">{labels[choice].title}</span>
                  <span className="picker-sub">{labels[choice].hint}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
