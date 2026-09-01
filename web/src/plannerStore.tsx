import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import * as api from './api'
import { PlannerContext, type PlannerStore } from './plannerContext'
import { nodeId } from './plannerGrid'
import { useStore, type Status } from './storeContext'
import type {
  FactoryResponse,
  FactoryTargetState,
  PlannerNode,
  PlannerStep,
  RateUnit,
} from './types'
import { usePersistent } from './usePersistent'

interface PlannerPersisted {
  nodes: PlannerNode[]
}

/** The pre-grid persisted shape: a step list plus a target list. */
interface LegacyPersisted {
  steps?: PlannerStep[]
  targets?: FactoryTargetState[]
}

const DEBOUNCE_MS = 400

/** A node rate in units per second. */
function rateOf(amount: number, window: number, windowUnit: RateUnit): number {
  const seconds = windowUnit === 'tick' ? window / 20 : windowUnit === 'minute' ? window * 60 : window
  return seconds > 0 ? amount / seconds : 0
}

/** A stored step list and target list become grid nodes in three loose columns; Tidy settles them. */
function migrate(raw: unknown): PlannerPersisted {
  const legacy = raw as (PlannerPersisted & LegacyPersisted) | null
  if (legacy?.nodes !== undefined) {
    return { nodes: legacy.nodes }
  }
  const nodes: PlannerNode[] = []
  ;(legacy?.steps ?? []).forEach((step, index) => {
    nodes.push({ ...step, scope: step.scope ?? null, kind: 'step', x: 420, y: index * 160 })
  })
  ;(legacy?.targets ?? []).forEach((target, index) => {
    if (target.kind === 'energy') {
      nodes.push({ kind: 'energy', amps: target.amps, tier: target.tier, euT: target.euT, x: 960, y: 400 + index * 120 })
    } else if (target.kind === 'consume') {
      nodes.push({ ...target, kind: 'input', x: 0, y: index * 120 })
    } else {
      nodes.push({ ...target, kind: 'output', x: 960, y: index * 120 })
    }
  })
  return { nodes }
}

/** What the grid asks of the engine; positions and labels stay out so drags never re-solve. */
function requestOf(nodes: PlannerNode[]) {
  const steps = nodes.flatMap((node) =>
    node.kind === 'step' ? [{ id: node.id, machineItemId: node.machineItemId, ocSteps: node.ocSteps }] : [])
  const supplies = nodes
    .flatMap((node) => (node.kind === 'input' && node.amount === null ? [node.itemId] : []))
    .toSorted()
  const targets: api.FactorySolveTarget[] = []
  for (const node of nodes) {
    if (node.kind === 'output') {
      targets.push({ kind: 'produce', itemId: node.itemId, rate: rateOf(node.amount, node.window, node.windowUnit) })
    } else if (node.kind === 'input' && node.amount !== null) {
      targets.push({ kind: 'consume', itemId: node.itemId, rate: rateOf(node.amount, node.window, node.windowUnit) })
    } else if (node.kind === 'energy') {
      targets.push({ kind: 'energy', rate: node.euT, generatorTier: node.tier })
    }
  }
  // Placing a farm node is the consent the scope toggles used to carry.
  const mobFarms = nodes.some((node) => node.kind === 'step' && node.scope === 'factory_mob')
  const bredSeeds = nodes.some((node) => node.kind === 'step' && node.scope === 'factory_bred')
  const solvable = targets.some((target) => target.rate > 0) && (steps.length > 0 || supplies.length > 0)
  return { steps, supplies, targets, mobFarms, bredSeeds, solvable }
}

export function PlannerProvider({ children }: { children: ReactNode }) {
  const { garage, b, weights, pushToast } = useStore()
  const [raw, setRaw] = usePersistent<unknown>('gtnhp.planner', null)
  const state = migrate(raw)
  const [plan, setPlan] = useState<FactoryResponse | null>(null)
  const [status, setStatus] = useState<Status>({ phase: 'idle' })
  const generation = useRef(0)

  const setNodes = useCallback(
    (next: PlannerNode[] | ((previous: PlannerNode[]) => PlannerNode[])) =>
      setRaw((previous: unknown) => ({
        nodes: typeof next === 'function' ? next(migrate(previous).nodes) : next,
      })),
    [setRaw],
  )

  // The live loop keys on the derived request, so drags and renames re-render but never re-solve.
  const requestKey = JSON.stringify(requestOf(state.nodes))
  useEffect(() => {
    const { steps, supplies, targets, mobFarms, bredSeeds, solvable } = JSON.parse(requestKey) as ReturnType<typeof requestOf>
    if (!solvable) {
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
          garage, b, weights, targets, ['machines', 'resource', 'energy'], {},
          mobFarms, bredSeeds, steps, supplies)
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
  }, [requestKey, garage, b, weights, pushToast])

  const value: PlannerStore = {
    nodes: state.nodes,
    addNode: (node) =>
      setNodes((nodes) => (nodes.some((existing) => nodeId(existing) === nodeId(node)) ? nodes : [...nodes, node])),
    updateNode: (id, next) => setNodes((nodes) => nodes.map((node) => (nodeId(node) === id ? next : node))),
    removeNode: (id) => setNodes((nodes) => nodes.filter((node) => nodeId(node) !== id)),
    moveNode: (id, x, y) =>
      setNodes((nodes) => nodes.map((node) => (nodeId(node) === id ? { ...node, x, y } : node))),
    setNodes: (nodes) => setNodes(nodes),
    plan,
    status,
  }

  return <PlannerContext.Provider value={value}>{children}</PlannerContext.Provider>
}
