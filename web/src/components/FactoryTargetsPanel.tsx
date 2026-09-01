import { useFactory } from '../factoryContext'
import { useStore } from '../storeContext'
import { SearchBox } from './SearchBox'
import { TargetsEditor } from './TargetsEditor'

const PRIORITIES: { value: string; label: string }[] = [
  { value: '', label: 'resource → energy → machines' },
  { value: 'resource,machines,energy', label: 'resource → machines → energy' },
  { value: 'energy,resource,machines', label: 'energy → resource → machines' },
  { value: 'energy,machines,resource', label: 'energy → machines → resource' },
  { value: 'machines,resource,energy', label: 'machines → resource → energy' },
  { value: 'machines,energy,resource', label: 'machines → energy → resource' },
]

/** The layer-priority picker of the factory targets. */
function PrioritySelect({
  priority,
  setPriority,
}: {
  priority: string[]
  setPriority: (priority: string[]) => void
}) {
  return (
    <label className="garage-toggle" title="Layer order of the lexicographic objective">
      <span>Priority</span>
      <select
        value={priority.join(',')}
        onChange={(event) =>
          setPriority(event.target.value === '' ? [] : event.target.value.split(','))
        }
      >
        {PRIORITIES.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  )
}

export function FactoryTargetsPanel() {
  const { pins, setPin, names } = useStore()
  const factory = useFactory()
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
      ) : null}
      <TargetsEditor store={factory} />
      <PrioritySelect priority={factory.priority} setPriority={factory.setPriority} />
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
