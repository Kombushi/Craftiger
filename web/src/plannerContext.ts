import { createContext, useContext } from 'react'
import type { Status } from './storeContext'
import type { FactoryResponse, PlannerNode } from './types'

/** The Planner grid: user-placed nodes are the whole document, edges come from the live solve. */
export interface PlannerStore {
  nodes: PlannerNode[]
  /** Adds unless a node with the same identity already sits on the grid. */
  addNode: (node: PlannerNode) => void
  updateNode: (id: string, next: PlannerNode) => void
  removeNode: (id: string) => void
  moveNode: (id: string, x: number, y: number) => void
  /** Replaces the whole grid — the Factory import and the Tidy button. */
  setNodes: (nodes: PlannerNode[]) => void
  /** The live plan; re-solved automatically, debounced, whenever the derived request changes. */
  plan: FactoryResponse | null
  status: Status
}

export const PlannerContext = createContext<PlannerStore | null>(null)

export function usePlanner(): PlannerStore {
  const store = useContext(PlannerContext)
  if (store === null) {
    throw new Error('usePlanner requires a PlannerProvider')
  }
  return store
}
