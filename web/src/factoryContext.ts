import { createContext, useContext } from 'react'
import type { Status } from './storeContext'
import type { FactoryResponse, FactoryTargetState } from './types'

export interface FactoryStore {
  targets: FactoryTargetState[]
  addItemTarget: (target: { itemId: string; name: string; atlasIdx: number }) => void
  addEnergyTarget: () => void
  updateTarget: (index: number, next: FactoryTargetState) => void
  removeTarget: (index: number) => void
  mobFarms: boolean
  setMobFarms: (on: boolean) => void
  bredSeeds: boolean
  setBredSeeds: (on: boolean) => void
  /** Layer order for the lexicographic solve; empty means resource, energy, machines. */
  priority: string[]
  setPriority: (priority: string[]) => void
  plan: FactoryResponse | null
  status: Status
  stale: boolean
  solve: () => void
}

export const FactoryContext = createContext<FactoryStore | null>(null)

export function useFactory(): FactoryStore {
  const store = useContext(FactoryContext)
  if (store === null) {
    throw new Error('useFactory requires a FactoryProvider')
  }
  return store
}

/** A target's request rate in units per second. */
export function ratePerSecond(target: FactoryTargetState): number {
  if (target.kind === 'energy') {
    return target.euT
  }
  const seconds =
    target.windowUnit === 'tick'
      ? target.window / 20
      : target.windowUnit === 'minute'
        ? target.window * 60
        : target.window
  return seconds > 0 ? target.amount / seconds : 0
}
