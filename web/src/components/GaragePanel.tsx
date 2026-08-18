import { useEffect, useMemo, useState } from 'react'
import * as api from '../api'
import { fmtHeat } from '../format'
import { useStore } from '../storeContext'
import type { MachineDto } from '../types'

export function GaragePanel() {
  const { meta, cart, garage, setGarage } = useStore()
  const [relevant, setRelevant] = useState<string[]>([])
  const [showAll, setShowAll] = useState(false)
  const cartKey = cart
    .map((entry) => entry.itemId)
    .sort()
    .join(',')

  useEffect(() => {
    if (cartKey === '') {
      setRelevant([])
      return
    }
    let live = true
    api
      .machinesFor(cartKey.split(','))
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
  }, [cartKey])

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

  const setTier = (name: string, value: string) => {
    const machines = { ...garage.machines }
    if (value === 'default') {
      delete machines[name]
    } else {
      machines[name] = value === 'none' ? null : Number(value)
    }
    setGarage({ ...garage, machines })
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
          onChange={(event) => setGarage({ ...garage, defaultTier: Number(event.target.value) })}
        >
          {meta.tierNames.map((tier, index) => (
            <option key={tier} value={index}>
              {tier}
            </option>
          ))}
        </select>
      </div>
      {cart.length === 0 && !showAll ? (
        <p className="hint">Machines used by your craft list appear here.</p>
      ) : null}
      <ul className="garage-list">
        {rows.map((machine) => (
          <MachineRow
            key={machine.name}
            machine={machine}
            defaultTier={garage.defaultTier}
            tierNames={meta.tierNames}
            coils={meta.coils}
            value={machine.name in garage.machines ? garage.machines[machine.name] : undefined}
            coil={garage.coils[machine.name] ?? ''}
            built={garage.builtMultiblocks.includes(machine.name)}
            onTier={(value) => setTier(machine.name, value)}
            onCoil={(value) => setCoil(machine.name, value)}
            onBuilt={(value) => setBuilt(machine.name, value)}
          />
        ))}
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
  const availability =
    machine.era === null
      ? 'Availability unknown — assumed owned at the default tier'
      : `First craftable at ${tierNames[machine.era] ?? machine.era}`
  return (
    <li className="garage-row">
      <span className="garage-name" title={`${machine.name} — ${availability}`}>
        {machine.name}
      </span>
      {machine.alwaysOwned ? (
        <span className="garage-owned">always owned</span>
      ) : (
        <select
          className={value === null || (value === undefined && lateByDefault) ? 'garage-none' : ''}
          value={value === undefined ? 'default' : value === null ? 'none' : String(value)}
          onChange={(event) => onTier(event.target.value)}
          title={availability}
        >
          <option value="default">{lateByDefault ? 'Not built' : 'Default'}</option>
          <option value="none">None</option>
          {tierNames.map((tier, index) => (
            <option key={tier} value={index}>
              {tier}
            </option>
          ))}
        </select>
      )}
      {machine.heatGated ? (
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