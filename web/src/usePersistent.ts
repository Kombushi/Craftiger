import { useEffect, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'

/** Component state mirrored into localStorage under the store's versioned envelope. */
export function usePersistent<T>(key: string, fallback: T): [T, Dispatch<SetStateAction<T>>] {
  const [value, setValue] = useState<T>(() => {
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
  })
  useEffect(() => {
    localStorage.setItem(key, JSON.stringify({ v: 1, data: value }))
  }, [key, value])
  return [value, setValue]
}
