import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react'
import * as api from './api'
import { FactoryContext, ratePerSecond, type FactoryStore } from './factoryContext'
import { useStore, type Status } from './storeContext'
import type { FactoryResponse, FactoryTargetState } from './types'
import { usePersistent } from './usePersistent'

interface FactoryPersisted {
  targets: FactoryTargetState[]
  mobFarms: boolean
  bredSeeds: boolean
  priority: string[]
}

const empty: FactoryPersisted = { targets: [], mobFarms: false, bredSeeds: false, priority: [] }

function sorted<T>(record: Record<string, T>): [string, T][] {
  return Object.entries(record).sort(([a], [b]) => (a < b ? -1 : 1))
}

/** The plan is stale when anything that travels in the request drifts from what solved it. */
function factoryKey(
  state: FactoryPersisted,
  garage: ReturnType<typeof useStore>['garage'],
  b: number,
  weights: Record<string, number>,
  pins: Record<string, string>,
): string {
  return JSON.stringify({
    targets: state.targets.map((target) =>
      target.kind === 'energy'
        ? ['energy', target.euT, target.tier]
        : [target.kind, target.itemId, ratePerSecond(target)],
    ),
    garage: {
      defaultTier: garage.defaultTier,
      machines: sorted(garage.machines),
      builtMultiblocks: [...garage.builtMultiblocks].sort(),
      coils: sorted(garage.coils),
    },
    b,
    weights: sorted(weights),
    pins: sorted(pins),
    mobFarms: state.mobFarms,
    bredSeeds: state.bredSeeds,
    priority: state.priority,
  })
}

export function FactoryProvider({ children }: { children: ReactNode }) {
  const { meta, garage, b, weights, pins, pushToast } = useStore()
  const [state, setState] = usePersistent<FactoryPersisted>('gtnhp.factory', empty)
  const [plan, setPlan] = useState<FactoryResponse | null>(null)
  const [status, setStatus] = useState<Status>({ phase: 'idle' })
  const [appliedKey, setAppliedKey] = useState<string | null>(null)
  const generation = useRef(0)

  const solve = useCallback(() => {
    const run = ++generation.current
    const key = factoryKey(state, garage, b, weights, pins)
    setStatus({ phase: 'solving' })
    api
      .factorySolve(
        garage,
        b,
        weights,
        state.targets.map((target) =>
          target.kind === 'energy'
            ? { kind: 'energy' as const, rate: target.euT, generatorTier: target.tier }
            : { kind: target.kind, itemId: target.itemId, rate: ratePerSecond(target) },
        ),
        state.priority,
        pins,
        state.mobFarms,
        state.bredSeeds,
      )
      .then((solved) => {
        if (run === generation.current) {
          setPlan(solved)
          setStatus({ phase: 'done' })
          setAppliedKey(key)
        }
      })
      .catch((error: unknown) => {
        if (run === generation.current) {
          setStatus({ phase: 'idle' })
          pushToast(error instanceof Error ? error.message : String(error))
        }
      })
  }, [state, garage, b, weights, pins, pushToast])

  const addItemTarget = useCallback(
    (target: { itemId: string; name: string; atlasIdx: number }) => {
      setState((previous) =>
        previous.targets.some(
          (existing) => existing.kind !== 'energy' && existing.itemId === target.itemId,
        )
          ? previous
          : {
              ...previous,
              targets: [
                ...previous.targets,
                { kind: 'produce', ...target, amount: 1, window: 1, windowUnit: 'second' },
              ],
            },
      )
    },
    [setState],
  )

  const addEnergyTarget = useCallback(() => {
    setState((previous) => {
      if (previous.targets.some((target) => target.kind === 'energy')) {
        return previous
      }
      const tier = Math.max(1, garage.defaultTier)
      const euT = meta?.tierVoltages[tier] ?? 32
      return { ...previous, targets: [...previous.targets, { kind: 'energy', amps: 1, tier, euT }] }
    })
  }, [setState, garage.defaultTier, meta])

  const stale = useMemo(
    () => plan !== null && appliedKey !== null && appliedKey !== factoryKey(state, garage, b, weights, pins),
    [plan, appliedKey, state, garage, b, weights, pins],
  )

  const value: FactoryStore = {
    targets: state.targets,
    addItemTarget,
    addEnergyTarget,
    updateTarget: (index, next) =>
      setState((previous) => ({
        ...previous,
        targets: previous.targets.map((target, at) => (at === index ? next : target)),
      })),
    removeTarget: (index) =>
      setState((previous) => ({
        ...previous,
        targets: previous.targets.filter((_, at) => at !== index),
      })),
    mobFarms: state.mobFarms,
    setMobFarms: (on) => setState((previous) => ({ ...previous, mobFarms: on })),
    bredSeeds: state.bredSeeds,
    setBredSeeds: (on) => setState((previous) => ({ ...previous, bredSeeds: on })),
    priority: state.priority,
    setPriority: (priority) => setState((previous) => ({ ...previous, priority })),
    plan,
    status,
    stale,
    solve,
  }

  return <FactoryContext.Provider value={value}>{children}</FactoryContext.Provider>
}
