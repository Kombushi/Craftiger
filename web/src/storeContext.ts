import { createContext, useContext } from 'react'
import type { BomResponse, CartEntry, GarageState, MetaResponse } from './types'

export interface NameRef {
  name: string
  atlasIdx: number
  isFluid: boolean
}

export interface Results {
  solveId: string
  pricedItems: number
  converged: boolean
  cart: BomResponse
  perTarget: Record<string, BomResponse>
}

export type Status = { phase: 'idle' } | { phase: 'solving' } | { phase: 'done' }

export interface Toast {
  id: number
  message: string
}

export interface Store {
  meta: MetaResponse | null
  toasts: Toast[]
  pushToast: (message: string) => void
  dismissToast: (id: number) => void
  cart: CartEntry[]
  addToCart: (entry: Omit<CartEntry, 'count'>) => void
  setCount: (itemId: string, count: number) => void
  removeFromCart: (itemId: string) => void
  garage: GarageState
  setGarage: (garage: GarageState) => void
  b: number
  setB: (b: number) => void
  weights: Record<string, number>
  setWeights: (weights: Record<string, number>) => void
  pins: Record<string, string>
  setPin: (itemId: string, recipeId: string | null) => void
  names: Record<string, NameRef>
  rememberNames: (names: Record<string, NameRef>) => void
  results: Results | null
  status: Status
  stale: boolean
  calculate: () => void
  hideUnreachable: boolean
  setHideUnreachable: (hide: boolean) => void
  detailItemId: string | null
  openDetail: (itemId: string) => void
  closeDetail: () => void
}

export const StoreContext = createContext<Store | null>(null)

export function useStore(): Store {
  const store = useContext(StoreContext)
  if (store === null) {
    throw new Error('useStore requires a StoreProvider')
  }
  return store
}