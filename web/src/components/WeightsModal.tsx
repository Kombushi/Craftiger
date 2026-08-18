import { useStore } from '../storeContext'
import { SearchBox } from './SearchBox'
import { Slot } from './Slot'
import { Stepper } from './Stepper'

interface Props {
  onClose: () => void
}

/** Per-item leaf weight overrides and the price base B — solve inputs, applied on Calculate. */
export function WeightsModal({ onClose }: Props) {
  const { b, setB, weights, setWeights, names, rememberNames } = useStore()

  const setWeight = (itemId: string, weight: number) => {
    setWeights({ ...weights, [itemId]: weight })
  }

  const remove = (itemId: string) => {
    const next = { ...weights }
    delete next[itemId]
    setWeights(next)
  }

  return (
    <div className="overlay" onClick={onClose}>
      <div className="modal" onClick={(event) => event.stopPropagation()}>
        <header className="modal-head">
          <span className="panel-title">Leaf weights</span>
          <button type="button" className="ghost-button" onClick={onClose}>
            ×
          </button>
        </header>
        <div className="weights-b">
          <label htmlFor="weight-b" title="Ingot price base: an ingot of tier t costs B × 4^t">
            Price base B
          </label>
          <Stepper id="weight-b" min={0.1} step={0.5} value={b} onChange={setB} />
        </div>
        <SearchBox
          placeholder="Add an item override…"
          onPick={(item) => {
            rememberNames({
              [item.itemId]: { name: item.name, atlasIdx: item.atlasIdx, isFluid: false },
            })
            if (!(item.itemId in weights)) {
              setWeight(item.itemId, 1)
            }
          }}
        />
        {Object.keys(weights).length === 0 ? (
          <p className="hint">No overrides — every leaf uses its built-in weight.</p>
        ) : (
          <ul className="weights-list">
            {Object.entries(weights).map(([itemId, weight]) => {
              const ref = names[itemId]
              return (
                <li key={itemId} className="weights-row">
                  {ref ? <Slot atlasIdx={ref.atlasIdx} size="sm" /> : null}
                  <span className="cart-name" title={ref?.name ?? itemId}>
                    {ref?.name ?? itemId}
                  </span>
                  <Stepper
                    className="weights-input"
                    min={0}
                    step={0.5}
                    value={weight}
                    onChange={(value) => setWeight(itemId, value)}
                  />
                  <button
                    type="button"
                    className="ghost-button"
                    title="Remove override"
                    onClick={() => remove(itemId)}
                  >
                    ×
                  </button>
                </li>
              )
            })}
          </ul>
        )}
        <p className="hint">Weights apply on the next Calculate.</p>
      </div>
    </div>
  )
}