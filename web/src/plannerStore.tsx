import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import * as api from './api'
import { ratePerSecond } from './factoryContext'
import { PlannerContext, type PlannerStore } from './plannerContext'
import { useStore, type Status } from './storeContext'
import type { FactoryResponse, FactoryTargetState, PlannerStep } from './types'
import { usePersistent } from './usePersistent'

interface PlannerPersisted {
  steps: PlannerStep[]
  targets: FactoryTargetState[]
  mobFarms: boolean
  bredSeeds: boolean
  priority: string[]
}

const empty: PlannerPersisted = { steps: [], targets: [], mobFarms: false, bredSeeds: false, priority: [] }

const DEBOUNCE_MS = 400

export function PlannerProvider({ children }: { children: ReactNode }) {
  const { meta, garage, b, weights, pushToast } = useStore()
  const [state, setState] = usePersistent<PlannerPersisted>('gtnhp.planner', empty)
  const [plan, setPlan] = useState<FactoryResponse | null>(null)
  const [status, setStatus] = useState<Status>({ phase: 'idle' })
  const generation = useRef(0)

  // The live loop: hand-picked models solve in milliseconds, so every edit re-solves after a
  // breath; only the first solve after a garage or weights change pays for the cost solve.
  useEffect(() => {
    if (state.steps.length === 0 || state.targets.length === 0) {
      generation.current++
      setPlan(null)
      setStatus({ phase: 'idle' })
      return
    }
    const run = ++generation.current
    setStatus({ phase: 'solving' })
    const timer = setTimeout(() => {
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
          {},
          state.mobFarms,
          state.bredSeeds,
          state.steps.map((step) => ({
            id: step.id,
            machineItemId: step.machineItemId,
            ocSteps: step.ocSteps,
          })),
        )
        .then((solved) => {
          if (run === generation.current) {
            setPlan(solved)
            setStatus({ phase: 'done' })
          }
        })
        .catch((error: unknown) => {
          if (run === generation.current) {
            setStatus({ phase: 'idle' })
            pushToast(error instanceof Error ? error.message : String(error))
          }
        })
    }, DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [state, garage, b, weights, pushToast])

  const addStep = useCallback(
    (step: PlannerStep) => {
      setState((previous) =>
        previous.steps.some((existing) => existing.id === step.id)
          ? previous
          : { ...previous, steps: [...previous.steps, step] },
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

  const value: PlannerStore = {
    steps: state.steps,
    addStep,
    updateStep: (index, next) =>
      setState((previous) => ({
        ...previous,
        steps: previous.steps.map((step, at) => (at === index ? next : step)),
      })),
    removeStep: (index) =>
      setState((previous) => ({ ...previous, steps: previous.steps.filter((_, at) => at !== index) })),
    setSteps: (steps) => setState((previous) => ({ ...previous, steps })),
    targets: state.targets,
    addItemTarget: (target) =>
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
      ),
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
  }

  return <PlannerContext.Provider value={value}>{children}</PlannerContext.Provider>
}
