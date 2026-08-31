import { createContext, useContext } from 'react'
import type { TargetsStore } from './factoryContext'
import type { Status } from './storeContext'
import type { FactoryResponse, PlannerStep } from './types'

export interface PlannerStore extends TargetsStore {
  steps: PlannerStep[]
  addStep: (step: PlannerStep) => void
  updateStep: (index: number, next: PlannerStep) => void
  removeStep: (index: number) => void
  /** Replaces the whole list — the "start from the Factory plan" import. */
  setSteps: (steps: PlannerStep[]) => void
  mobFarms: boolean
  setMobFarms: (on: boolean) => void
  bredSeeds: boolean
  setBredSeeds: (on: boolean) => void
  priority: string[]
  setPriority: (priority: string[]) => void
  /** The live plan; re-solved automatically, debounced, on every edit. */
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
