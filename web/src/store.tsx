import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import * as api from './api'
import { ApiError } from './api'
import { StoreContext, type NameRef, type Results, type Status, type Store, type Toast } from './storeContext'
import type { CartEntry, GarageState, MetaResponse } from './types'

interface UiState {
  names: Record<string, NameRef>
  hideUnreachable: boolean
  applied: { solveId: string; pricedItems: number; converged: boolean; key: string } | null
}

function load<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    if (!raw) {
      return fallback
    }
    const parsed = JSON.parse(raw) as { v: number; data: T }
    return parsed.v === 1 ? parsed.data : fallback
  } catch {
    return fallback
  }
}

function persist(key: string, data: unknown): void {
  localStorage.setItem(key, JSON.stringify({ v: 1, data }))
}

const defaultGarage: GarageState = {
  defaultTier: 3,
  machines: {},
  builtMultiblocks: [],
  coils: {},
}

/** Machines the artifact renamed; stored garage keys follow so a tier or coil survives the rebuild. */
const machineRenames: Record<string, string> = { 'Blast Furnace': 'Electric Blast Furnace' }
const coilRenames: Record<string, string> = { 'TPV-Alloy': 'TPV' }

/** Renames stored keys only once the live machine list carries the new name and not the old one. */
function migrateGarage(garage: GarageState, live: Set<string>, liveCoils: Set<string>): GarageState {
  const rename = (name: string) => {
    const renamed = machineRenames[name]
    return renamed !== undefined && live.has(renamed) && !live.has(name) ? renamed : name
  }
  const renameCoil = (name: string) => {
    const renamed = coilRenames[name]
    return renamed !== undefined && liveCoils.has(renamed) && !liveCoils.has(name) ? renamed : name
  }
  const keys = [...Object.keys(garage.machines), ...Object.keys(garage.coils), ...garage.builtMultiblocks]
  if (
    keys.every((name) => rename(name) === name) &&
    Object.values(garage.coils).every((name) => renameCoil(name) === name)
  ) {
    return garage
  }
  const renameKeys = <T,>(record: Record<string, T>) =>
    Object.fromEntries(Object.entries(record).map(([name, value]) => [rename(name), value]))
  return {
    ...garage,
    machines: renameKeys(garage.machines),
    coils: Object.fromEntries(
      Object.entries(garage.coils).map(([map, coil]) => [rename(map), renameCoil(coil)]),
    ),
    builtMultiblocks: [...new Set(garage.builtMultiblocks.map(rename))].sort(),
  }
}

/** Everything a solve depends on; results are stale when this drifts from the applied key. */
function settingsKey(
  cart: CartEntry[],
  garage: GarageState,
  b: number,
  weights: Record<string, number>,
): string {
  return JSON.stringify({
    cart: cart.map((entry) => [entry.itemId, entry.count]),
    garage: {
      defaultTier: garage.defaultTier,
      machines: sorted(garage.machines),
      builtMultiblocks: [...garage.builtMultiblocks].sort(),
      coils: sorted(garage.coils),
    },
    b,
    weights: sorted(weights),
  })
}

function sorted<T>(record: Record<string, T>): [string, T][] {
  return Object.entries(record).sort(([a], [b]) => (a < b ? -1 : 1))
}

export function StoreProvider({ children }: { children: ReactNode }) {
  const [meta, setMeta] = useState<MetaResponse | null>(null)
  const [toasts, setToasts] = useState<Toast[]>([])
  const toastId = useRef(0)
  const [cart, setCart] = useState<CartEntry[]>(() => load('gtnhp.cart', []))
  const [garage, setGarage] = useState<GarageState>(() => load('gtnhp.machines', defaultGarage))
  const [config, setConfig] = useState<{ b: number }>(() => load('gtnhp.config', { b: 4 }))
  const [weights, setWeights] = useState<Record<string, number>>(() => load('gtnhp.weights', {}))
  const [pins, setPins] = useState<Record<string, string>>(() => load('gtnhp.pins', {}))
  const [ui, setUi] = useState<UiState>(() =>
    load('gtnhp.ui', { names: {}, hideUnreachable: false, applied: null }),
  )
  const [results, setResults] = useState<Results | null>(null)
  const [status, setStatus] = useState<Status>({ phase: 'idle' })
  const [detailItemId, setDetailItemId] = useState<string | null>(null)
  const generation = useRef(0)

  const dismissToast = useCallback((id: number) => {
    setToasts((previous) => previous.filter((toast) => toast.id !== id))
  }, [])

  const pushToast = useCallback((message: string) => {
    const id = ++toastId.current
    setToasts((previous) => [...previous.slice(-3), { id, message }])
  }, [])

  // An unreachable API announces itself once and keeps retrying quietly.
  useEffect(() => {
    let live = true
    let announced = false
    const attempt = () => {
      api
        .getMeta()
        .then((fetched) => {
          if (live) {
            setMeta(fetched)
          }
        })
        .catch((error: unknown) => {
          if (!live) {
            return
          }
          if (!announced) {
            announced = true
            pushToast(
              `The planner API is not reachable: ${error instanceof Error ? error.message : String(error)}`,
            )
          }
          window.setTimeout(() => {
            if (live) {
              attempt()
            }
          }, 5000)
        })
    }
    attempt()
    return () => {
      live = false
    }
  }, [pushToast])

  useEffect(() => {
    if (meta !== null) {
      const live = new Set(meta.machines.map((machine) => machine.name))
      const liveCoils = new Set(meta.coils.map((coil) => coil.name))
      setGarage((current) => migrateGarage(current, live, liveCoils))
    }
  }, [meta])
  useEffect(() => persist('gtnhp.cart', cart), [cart])
  useEffect(() => persist('gtnhp.machines', garage), [garage])
  useEffect(() => persist('gtnhp.config', config), [config])
  useEffect(() => persist('gtnhp.weights', weights), [weights])
  useEffect(() => persist('gtnhp.pins', pins), [pins])
  useEffect(() => persist('gtnhp.ui', ui), [ui])

  const fetchBoms = useCallback(
    async (solveId: string, targets: CartEntry[], activePins: Record<string, string>) => {
      const all = targets.map((entry) => ({ itemId: entry.itemId, count: entry.count }))
      const [cartBom, ...perTarget] = await Promise.all([
        api.bom(solveId, all, activePins),
        ...all.map((target) => api.bom(solveId, [target], activePins)),
      ])
      return {
        cart: cartBom,
        perTarget: Object.fromEntries(targets.map((entry, i) => [entry.itemId, perTarget[i]])),
      }
    },
    [],
  )

  const runSolve = useCallback(
    async (targets: CartEntry[], activePins: Record<string, string>) => {
      const run = ++generation.current
      setStatus({ phase: 'solving' })
      try {
        const solved = await api.solve(garage, config.b, weights)
        const boms = await fetchBoms(solved.solveId, targets, activePins)
        if (run !== generation.current) {
          return
        }
        setResults({ ...solved, ...boms })
        setStatus({ phase: 'done' })
        setUi((previous) => ({
          ...previous,
          applied: {
            solveId: solved.solveId,
            pricedItems: solved.pricedItems,
            converged: solved.converged,
            key: settingsKey(targets, garage, config.b, weights),
          },
        }))
      } catch (error) {
        if (run === generation.current) {
          setStatus({ phase: 'idle' })
          pushToast(error instanceof Error ? error.message : String(error))
        }
      }
    },
    [garage, config.b, weights, fetchBoms, pushToast],
  )

  const calculate = useCallback(() => {
    void runSolve(cart, pins)
  }, [runSolve, cart, pins])

  // A pin changes the walk, not the solve: refresh the BOMs on the live solveId and fall
  // back to a full run only when the cache entry is gone.
  const setPin = useCallback(
    (itemId: string, recipeId: string | null) => {
      const next = { ...pins }
      if (recipeId === null) {
        delete next[itemId]
      } else {
        next[itemId] = recipeId
      }
      setPins(next)
      if (results === null) {
        return
      }
      const run = ++generation.current
      fetchBoms(results.solveId, cart, next)
        .then((boms) => {
          if (run === generation.current) {
            setResults({ ...results, ...boms })
          }
        })
        .catch((error: unknown) => {
          if (error instanceof ApiError && error.status === 404) {
            void runSolve(cart, next)
          } else if (run === generation.current) {
            pushToast(error instanceof Error ? error.message : String(error))
          }
        })
    },
    [pins, results, cart, fetchBoms, runSolve, pushToast],
  )

  // Reload with unchanged settings resumes on the cached solve instead of asking for a
  // recalculation; an evicted solveId just leaves the planner idle.
  const resumed = useRef(false)
  useEffect(() => {
    if (resumed.current || ui.applied === null) {
      return
    }
    resumed.current = true
    const applied = ui.applied
    if (applied.key !== settingsKey(cart, garage, config.b, weights) || cart.length === 0) {
      return
    }
    const run = ++generation.current
    setStatus({ phase: 'solving' })
    fetchBoms(applied.solveId, cart, pins)
      .then((boms) => {
        if (run === generation.current) {
          setResults({
            solveId: applied.solveId,
            pricedItems: applied.pricedItems,
            converged: applied.converged,
            ...boms,
          })
          setStatus({ phase: 'done' })
        }
      })
      .catch(() => {
        if (run === generation.current) {
          setStatus({ phase: 'idle' })
        }
      })
  }, [ui.applied, cart, garage, config.b, weights, pins, fetchBoms])

  const rememberNames = useCallback((incoming: Record<string, NameRef>) => {
    setUi((previous) => {
      let changed = false
      const names = { ...previous.names }
      for (const [id, ref] of Object.entries(incoming)) {
        if (!(id in names)) {
          names[id] = ref
          changed = true
        }
      }
      return changed ? { ...previous, names } : previous
    })
  }, [])

  const addToCart = useCallback((entry: Omit<CartEntry, 'count'>) => {
    setCart((previous) =>
      previous.some((existing) => existing.itemId === entry.itemId)
        ? previous
        : [...previous, { ...entry, count: 1 }],
    )
  }, [])

  const setCount = useCallback((itemId: string, count: number) => {
    setCart((previous) =>
      previous.map((entry) => (entry.itemId === itemId ? { ...entry, count } : entry)),
    )
  }, [])

  const removeFromCart = useCallback((itemId: string) => {
    setCart((previous) => previous.filter((entry) => entry.itemId !== itemId))
  }, [])

  const stale = useMemo(
    () =>
      results !== null &&
      ui.applied !== null &&
      ui.applied.key !== settingsKey(cart, garage, config.b, weights),
    [results, ui.applied, cart, garage, config.b, weights],
  )

  const value: Store = {
    meta,
    toasts,
    pushToast,
    dismissToast,
    cart,
    addToCart,
    setCount,
    removeFromCart,
    garage,
    setGarage,
    b: config.b,
    setB: (b) => setConfig({ b }),
    weights,
    setWeights,
    pins,
    setPin,
    names: ui.names,
    rememberNames,
    results,
    status,
    stale,
    calculate,
    hideUnreachable: ui.hideUnreachable,
    setHideUnreachable: (hideUnreachable) => setUi((previous) => ({ ...previous, hideUnreachable })),
    detailItemId,
    openDetail: setDetailItemId,
    closeDetail: () => setDetailItemId(null),
  }

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>
}