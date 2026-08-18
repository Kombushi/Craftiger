import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import { SearchBox } from './SearchBox'
import { Slot } from './Slot'
import { Stepper } from './Stepper'

export function CartPanel() {
  const { cart, addToCart, setCount, removeFromCart, results, openDetail } = useStore()

  return (
    <section className="panel">
      <header className="panel-title">Craft list</header>
      <SearchBox
        placeholder="Search an item to craft…"
        onPick={(item) =>
          addToCart({ itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx, isFluid: false })
        }
      />
      {cart.length === 0 ? (
        <p className="hint">Search an item above to start planning.</p>
      ) : (
        <ul className="cart">
          {cart.map((entry) => {
            const cost = results?.cart.items[entry.itemId]?.cost ?? null
            return (
              <li key={entry.itemId} className="cart-row">
                <Slot
                  atlasIdx={entry.atlasIdx}
                  size="sm"
                  title={entry.name}
                  onClick={() => openDetail(entry.itemId)}
                />
                <span className="cart-name" title={entry.name}>
                  {entry.name}
                </span>
                {results ? <span className="mono cart-cost">{fmtCost(cost)}</span> : null}
                <Stepper
                  className="cart-count"
                  min={1}
                  value={entry.count}
                  onChange={(count) => setCount(entry.itemId, Math.floor(count))}
                />
                <button
                  type="button"
                  className="ghost-button"
                  title="Remove from list"
                  onClick={() => removeFromCart(entry.itemId)}
                >
                  ×
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}