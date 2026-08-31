import { ratePerSecond, type TargetsStore } from '../factoryContext'
import { fmtRate } from '../format'
import { useStore } from '../storeContext'
import type { FactoryEnergyTarget, FactoryItemTarget, RateUnit } from '../types'
import { Slot } from './Slot'
import { Stepper } from './Stepper'

/** The rate-target rows and the energy-row adder, shared by the Factory and Planner sidebars. */
export function TargetsEditor({ store }: { store: TargetsStore }) {
  const { meta, openDetail } = useStore()
  const hasEnergy = store.targets.some((target) => target.kind === 'energy')
  return (
    <>
      {store.targets.length === 0 ? null : (
        <ul className="cart">
          {store.targets.map((target, index) =>
            target.kind === 'energy' ? (
              <EnergyRow key="energy" store={store} target={target} index={index} />
            ) : (
              <ItemRow key={target.itemId} store={store} target={target} index={index} onOpen={openDetail} />
            ),
          )}
        </ul>
      )}
      {!hasEnergy ? (
        <button
          type="button"
          className="ghost-button target-add-energy"
          title="Ask for net exported power: the plan builds and feeds the generators"
          onClick={store.addEnergyTarget}
          disabled={meta === null}
        >
          + energy target
        </button>
      ) : null}
    </>
  )
}

function ItemRow({
  store,
  target,
  index,
  onOpen,
}: {
  store: TargetsStore
  target: FactoryItemTarget
  index: number
  onOpen: (itemId: string) => void
}) {
  const set = (next: Partial<FactoryItemTarget>) => store.updateTarget(index, { ...target, ...next })
  return (
    <li className={`cart-row target-row${target.kind === 'consume' ? ' target-consume' : ''}`}>
      <Slot
        atlasIdx={target.atlasIdx}
        size="sm"
        tooltip={{ name: target.name }}
        onClick={() => onOpen(target.itemId)}
      />
      <span className="cart-name" title={target.name}>
        {target.name}
      </span>
      <span className="seg">
        <button
          type="button"
          className={target.kind === 'produce' ? 'seg-active' : ''}
          title="Produce at this rate"
          onClick={() => set({ kind: 'produce' })}
        >
          OUT
        </button>
        <button
          type="button"
          className={target.kind === 'consume' ? 'seg-active' : ''}
          title="Absorb this incoming rate"
          onClick={() => set({ kind: 'consume' })}
        >
          IN
        </button>
      </span>
      <button
        type="button"
        className="ghost-button"
        title="Remove this target"
        onClick={() => store.removeTarget(index)}
      >
        ×
      </button>
      <span className="target-entry">
        <Stepper
          className="target-amount"
          min={1}
          value={target.amount}
          onChange={(amount) => set({ amount: Math.floor(amount) })}
        />
        <span className="target-per mono">/</span>
        <Stepper
          className="target-window"
          min={1}
          value={target.window}
          onChange={(window) => set({ window: Math.floor(window) })}
        />
        <select
          className="target-unit"
          value={target.windowUnit}
          onChange={(event) => set({ windowUnit: event.target.value as RateUnit })}
        >
          <option value="tick">t</option>
          <option value="second">s</option>
          <option value="minute">min</option>
        </select>
        <span className="target-rate mono">= {fmtRate(ratePerSecond(target), 'second')}</span>
      </span>
    </li>
  )
}

function EnergyRow({
  store,
  target,
  index,
}: {
  store: TargetsStore
  target: FactoryEnergyTarget
  index: number
}) {
  const { meta } = useStore()
  const voltages = meta?.tierVoltages ?? []
  const tierNames = meta?.tierNames ?? []
  const set = (next: Partial<FactoryEnergyTarget>) => store.updateTarget(index, { ...target, ...next })
  return (
    <li className="cart-row target-row target-energy">
      <span className="slot slot-sm slot-energy" title="Net exported power">
        ⚡
      </span>
      <span className="cart-name">Energy</span>
      <button
        type="button"
        className="ghost-button"
        title="Remove the energy target"
        onClick={() => store.removeTarget(index)}
      >
        ×
      </button>
      <span className="target-entry">
        <Stepper
          className="target-amount"
          min={1}
          value={target.amps}
          onChange={(amps) => {
            const whole = Math.floor(amps)
            set({ amps: whole, euT: whole * (voltages[target.tier] ?? 0) })
          }}
        />
        <span className="target-per mono">A ×</span>
        <select
          className="target-unit"
          title="The tier the exported power arrives at; generators below it do not count"
          value={target.tier}
          onChange={(event) => {
            const tier = Number(event.target.value)
            set({ tier, euT: target.amps * (voltages[tier] ?? 0) })
          }}
        >
          {tierNames.map((name, tier) =>
            tier > 0 ? (
              <option key={name} value={tier}>
                {name}
              </option>
            ) : null,
          )}
        </select>
        <span className="target-rate mono">
          ={' '}
          <Stepper
            className="target-eut"
            min={1}
            value={target.euT}
            onChange={(euT) => set({ euT: Math.floor(euT) })}
          />{' '}
          EU/t
        </span>
      </span>
    </li>
  )
}
