export type CanvasMenuChoice = 'input' | 'output' | 'step' | 'energy'

const entries: { choice: CanvasMenuChoice; label: string; hint: string }[] = [
  { choice: 'input', label: 'Input node', hint: 'A free source you have on hand' },
  { choice: 'output', label: 'Output node', hint: 'A rate the pipeline must produce' },
  { choice: 'step', label: 'Producing step…', hint: 'A recipe, farm or machine line' },
  { choice: 'energy', label: 'Energy node', hint: 'The export generator steps feed' },
]

/** The canvas's right-click menu: what a new node at the clicked spot becomes. */
export function CanvasMenu({
  at,
  hasEnergy,
  onPick,
  onClose,
}: {
  at: { x: number; y: number }
  hasEnergy: boolean
  onPick: (choice: CanvasMenuChoice) => void
  onClose: () => void
}) {
  return (
    <div
      className="overlay overlay-clear"
      onClick={onClose}
      onContextMenu={(event) => {
        event.preventDefault()
        onClose()
      }}
    >
      <ul className="context-menu" style={{ left: at.x, top: at.y }} onClick={(event) => event.stopPropagation()}>
        {entries.map((entry) => (
          <li key={entry.choice}>
            <button
              type="button"
              disabled={entry.choice === 'energy' && hasEnergy}
              title={entry.choice === 'energy' && hasEnergy ? 'The grid already has its energy node' : undefined}
              onClick={() => onPick(entry.choice)}
            >
              <span>{entry.label}</span>
              <span className="context-hint">{entry.hint}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
