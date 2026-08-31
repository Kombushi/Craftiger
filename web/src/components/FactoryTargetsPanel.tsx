import { useFactory } from '../factoryContext'
import { ratePerSecond } from '../factoryContext'
import { fmtRate } from '../format'
import { useStore } from '../storeContext'
import type { FactoryEnergyTarget, FactoryItemTarget, RateUnit } from '../types'
import { SearchBox } from './SearchBox'
import { Slot } from './Slot'
import { Stepper } from './Stepper'

const PRIORITIES: { value: string; label: string }[] = [
  { value: '', label: 'resource → energy → machines' },
  { value: 'resource,machines,energy', label: 'resource → machines → energy' },
  { value: 'energy,resource,machines', label: 'energy → resource → machines' },
  { value: 'energy,machines,resource', label: 'energy → machines → resource' },
  { value: 'machines,resource,energy', label: 'machines → resource → energy' },
  { value: 'machines,energy,resource', label: 'machines → energy → resource' },
]

export function FactoryTargetsPanel() {
  const { meta, pins, setPin, names, openDetail } = useStore()
  const factory = useFactory()
  const hasEnergy = factory.targets.some((target) => target.kind === 'energy')
  const pinList = Object.keys(pins).sort()

  return (
    <section className="panel">
      <header className="panel-title">Factory targets</header>
      <SearchBox
        placeholder="Search an item to produce…"
        onPick={(item) =>
          factory.addItemTarget({ itemId: item.itemId, name: item.name, atlasIdx: item.atlasIdx })
        }
      />
      {factory.targets.length === 0 ? (
        <p className="hint">Search an item above, or add an energy target, to start planning rates.</p>
      ) : (
        <ul className="cart">
          {factory.targets.map((target, index) =>
            target.kind === 'energy' ? (
              <EnergyRow key="energy" target={target} index={index} />
            ) : (
              <ItemRow key={target.itemId} target={target} index={index} onOpen={openDetail} />
            ),
          )}
        </ul>
      )}
      {!hasEnergy ? (
        <button
          type="button"
          className="ghost-button target-add-energy"
          title="Ask for net exported power: the plan builds and feeds the generators"
          onClick={factory.addEnergyTarget}
          disabled={meta === null}
        >
          + energy target
        </button>
      ) : null}
      <label className="garage-toggle" title="Layer order of the lexicographic objective">
        <span>Priority</span>
        <select
          value={factory.priority.join(',')}
          onChange={(event) =>
            factory.setPriority(event.target.value === '' ? [] : event.target.value.split(','))
          }
        >
          {PRIORITIES.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </label>
      <label className="garage-toggle" title="Admit soul-vial mob crushers as machine lines">
        <input
          type="checkbox"
          checked={factory.mobFarms}
          onChange={(event) => factory.setMobFarms(event.target.checked)}
        />
        <span>Mob farms</span>
      </label>
      <label className="garage-toggle" title="Rate crop farms at bred 31/31 seeds instead of fresh 1/1">
        <input
          type="checkbox"
          checked={factory.bredSeeds}
          onChange={(event) => factory.setBredSeeds(event.target.checked)}
        />
        <span>Bred seeds</span>
      </label>
      {pinList.length > 0 ? (
        <>
          <header className="panel-title">Pins</header>
          <ul className="pins-row">
            {pinList.map((itemId) => (
              <li key={itemId} className="pin-row">
                <span className="cart-name" title={itemId}>
                  {names[itemId]?.name ?? itemId}
                </span>
                <button
                  type="button"
                  className="ghost-button"
                  title="Clear this pin"
                  onClick={() => setPin(itemId, null)}
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </section>
  )
}

function ItemRow({
  target,
  index,
  onOpen,
}: {
  target: FactoryItemTarget
  index: number
  onOpen: (itemId: string) => void
}) {
  const factory = useFactory()
  const set = (next: Partial<FactoryItemTarget>) =>
    factory.updateTarget(index, { ...target, ...next })
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
        onClick={() => factory.removeTarget(index)}
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

function EnergyRow({ target, index }: { target: FactoryEnergyTarget; index: number }) {
  const { meta } = useStore()
  const factory = useFactory()
  const voltages = meta?.tierVoltages ?? []
  const tierNames = meta?.tierNames ?? []
  const set = (next: Partial<FactoryEnergyTarget>) =>
    factory.updateTarget(index, { ...target, ...next })
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
        onClick={() => factory.removeTarget(index)}
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
