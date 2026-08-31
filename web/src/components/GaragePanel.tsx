import { useEffect, useMemo, useState } from 'react'
import * as api from '../api'
import { fmtHeat } from '../format'
import { useStore } from '../storeContext'
import type { MachineDto } from '../types'

/** The garage; relevance-filtered by the crafting cart unless the caller names its own target
 * items — the deep closure then walks through leaf-class items, the way a factory solve does. */
export function GaragePanel({ targetIds }: { targetIds?: string[] }) {
  const { meta, cart, garage, setGarage } = useStore()
  const [relevant, setRelevant] = useState<string[]>([])
  const [showAll, setShowAll] = useState(false)
  const deep = targetIds !== undefined
  const cartKey = (targetIds ?? cart.map((entry) => entry.itemId))
    .toSorted()
    .join(',')

  useEffect(() => {
    if (cartKey === '') {
      setRelevant([])
      return
    }
    let live = true
    api
      .machinesFor(cartKey.split(','), deep)
      .then((machines) => {
        if (live) {
          setRelevant(machines)
        }
      })
      .catch(() => {
        if (live) {
          setRelevant([])
        }
      })
    return () => {
      live = false
    }
  }, [cartKey, deep])

  const rows = useMemo(() => {
    if (!meta) {
      return []
    }
    const configured = new Set([
      ...Object.keys(garage.machines),
      ...garage.builtMultiblocks,
      ...Object.keys(garage.coils),
    ])
    const wanted = new Set([...relevant, ...configured])
    // The default garage cannot own late-era machines — hide them unless configured or showing all.
    const shown = (machine: MachineDto) =>
      showAll ||
      (wanted.has(machine.name) &&
        (configured.has(machine.name) ||
          machine.alwaysOwned ||
          machine.era === null ||
          machine.era <= garage.defaultTier))
    return meta.machines
      .filter(shown)
      .toSorted((a, b) => {
        if (a.alwaysOwned !== b.alwaysOwned) {
          return a.alwaysOwned ? 1 : -1
        }
        return a.name.localeCompare(b.name)
      })
  }, [meta, relevant, garage, showAll])

  if (!meta) {
    return null
  }

  const tierOf = (machines: Record<string, number | null>, name: string) =>
    name in machines ? machines[name] : undefined

  const setDefaultTier = (defaultTier: number) => {
    const coils = { ...garage.coils }
    for (const machine of meta.machines) {
      if (notBuilt(machine, tierOf(garage.machines, machine.name), defaultTier)) {
        delete coils[machine.name]
      }
    }
    setGarage({ ...garage, defaultTier, coils })
  }

  const setTier = (machine: MachineDto, value: string) => {
    const machines = { ...garage.machines }
    if (value === 'default') {
      delete machines[machine.name]
    } else {
      machines[machine.name] = value === 'none' ? null : Number(value)
    }
    const coils = { ...garage.coils }
    if (notBuilt(machine, tierOf(machines, machine.name), garage.defaultTier)) {
      delete coils[machine.name]
    }
    setGarage({ ...garage, machines, coils })
  }

  const setCoil = (name: string, coil: string) => {
    const coils = { ...garage.coils }
    if (coil === '') {
      delete coils[name]
    } else {
      coils[name] = coil
    }
    setGarage({ ...garage, coils })
  }

  const setBuilt = (name: string, built: boolean) => {
    const set = new Set(garage.builtMultiblocks)
    if (built) {
      set.add(name)
    } else {
      set.delete(name)
    }
    setGarage({ ...garage, builtMultiblocks: [...set].sort() })
  }

  return (
    <section className="panel panel-garage">
      <header className="panel-title">Machine garage</header>
      <div className="garage-default">
        <label htmlFor="default-tier">Default tier</label>
        <select
          id="default-tier"
          value={garage.defaultTier}
          onChange={(event) => setDefaultTier(Number(event.target.value))}
        >
          {meta.tierNames.map((tier, index) => (
            <option key={tier} value={index}>
              {tier}
            </option>
          ))}
        </select>
      </div>
      {cartKey === '' && !showAll ? (
        <p className="hint">Machines used by your targets appear here.</p>
      ) : null}
      <ul className="garage-list">
        {(() => {
          const row = (machine: MachineDto) => (
            <MachineRow
              key={machine.name}
              machine={machine}
              defaultTier={garage.defaultTier}
              tierNames={meta.tierNames}
              coils={meta.coils}
              value={tierOf(garage.machines, machine.name)}
              coil={garage.coils[machine.name] ?? ''}
              built={garage.builtMultiblocks.includes(machine.name)}
              onTier={(value) => setTier(machine, value)}
              onCoil={(value) => setCoil(machine.name, value)}
              onBuilt={(value) => setBuilt(machine.name, value)}
            />
          )
          const multis = rows.filter((machine) => machine.multiblockOnly)
          if (multis.length === 0) {
            return rows.map(row)
          }
          const singles = rows.filter((machine) => !machine.multiblockOnly)
          return (
            <>
              {singles.length > 0 ? <li className="garage-group-title">Machines</li> : null}
              {singles.map(row)}
              <li className="garage-group-title">Multiblocks</li>
              {multis.map(row)}
            </>
          )
        })()}
      </ul>
      <label className="garage-toggle">
        <input
          type="checkbox"
          checked={showAll}
          onChange={(event) => setShowAll(event.target.checked)}
        />
        Show all machines
      </label>
    </section>
  )
}

/** An explicit None/Not built, or inheriting a default tier the machine is not yet craftable at. */
function notBuilt(machine: MachineDto, value: number | null | undefined, defaultTier: number): boolean {
  if (machine.alwaysOwned) {
    return false
  }
  return value === null || (value === undefined && machine.era !== null && machine.era > defaultTier)
}

interface RowProps {
  machine: MachineDto
  defaultTier: number
  tierNames: string[]
  coils: { name: string; maxHeat: number }[]
  value: number | null | undefined
  coil: string
  built: boolean
  onTier: (value: string) => void
  onCoil: (value: string) => void
  onBuilt: (value: boolean) => void
}

function MachineRow({
  machine, defaultTier, tierNames, coils, value, coil, built, onTier, onCoil, onBuilt,
}: RowProps) {
  const lateByDefault =
    !machine.alwaysOwned && machine.era !== null && machine.era > defaultTier
  const absent = notBuilt(machine, value, defaultTier)
  const availability =
    machine.era === null
      ? 'Availability unknown — assumed owned at the default tier'
      : `First craftable at ${tierNames[machine.era] ?? machine.era}`
  return (
    <li className={machine.multiblockOnly ? 'garage-row garage-row-multi' : 'garage-row'}>
      <span className="garage-name" title={`${machine.name} — ${availability}`}>
        {machine.name}
      </span>
      {machine.alwaysOwned ? (
        <span className="garage-owned">always owned</span>
      ) : (
        <select
          className={absent ? 'garage-none' : ''}
          value={value === undefined ? 'default' : value === null ? 'none' : String(value)}
          onChange={(event) => onTier(event.target.value)}
          title={availability}
        >
          <option value="default">{lateByDefault ? 'Not built' : 'Default'}</option>
          <option value="none">{machine.multiblockOnly ? 'Not built' : 'None'}</option>
          {tierNames.map((tier, index) => (
            <option key={tier} value={index}>
              {machine.multiblockOnly ? `${tier} hatches` : tier}
            </option>
          ))}
        </select>
      )}
      {machine.heatGated && !absent ? (
        <select
          className="garage-coil"
          value={coil}
          onChange={(event) => onCoil(event.target.value)}
          title="Installed coils"
        >
          <option value="">No coils</option>
          {coils.map((option) => (
            <option key={option.name} value={option.name}>
              {option.name} ({fmtHeat(option.maxHeat)})
            </option>
          ))}
        </select>
      ) : null}
      {machine.hasMultiblockSwitch ? (
        <label className="garage-built" title="The multiblock version is built">
          <input
            type="checkbox"
            checked={built}
            onChange={(event) => onBuilt(event.target.checked)}
          />
          multi
        </label>
      ) : null}
    </li>
  )
}