import { useEffect, useRef, useState } from 'react'
import * as api from '../api'
import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import type { ItemSummary } from '../types'
import { Slot } from './Slot'

interface Props {
  placeholder: string
  onPick: (item: ItemSummary) => void
}

export function SearchBox({ placeholder, onPick }: Props) {
  const { results, rememberNames } = useStore()
  const [query, setQuery] = useState('')
  const [found, setFound] = useState<ItemSummary[]>([])
  const [open, setOpen] = useState(false)
  const [error, setError] = useState(false)
  const generation = useRef(0)
  const solveId = results?.solveId ?? null

  useEffect(() => {
    const trimmed = query.trim()
    if (trimmed.length < 2) {
      setFound([])
      setError(false)
      return
    }
    const run = ++generation.current
    const timer = setTimeout(() => {
      api
        .search(trimmed, solveId)
        .then((items) => {
          if (run === generation.current) {
            setFound(items)
            setError(false)
            rememberNames(
              Object.fromEntries(
                items.map((item) => [
                  item.itemId,
                  { name: item.name, atlasIdx: item.atlasIdx, isFluid: false },
                ]),
              ),
            )
          }
        })
        .catch(() => {
          if (run === generation.current) {
            setError(true)
          }
        })
    }, 200)
    return () => clearTimeout(timer)
  }, [query, solveId, rememberNames])

  return (
    <div className="search">
      <input
        className="search-input"
        type="text"
        value={query}
        placeholder={placeholder}
        onChange={(event) => {
          setQuery(event.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
      />
      {open && query.trim().length >= 2 ? (
        <div className="search-results">
          {error ? <div className="search-empty">Search failed — is the API running?</div> : null}
          {!error && found.length === 0 ? <div className="search-empty">No items match</div> : null}
          {found.map((item) => (
            <button
              key={item.itemId}
              type="button"
              className="search-row"
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => {
                onPick(item)
                setQuery('')
                setOpen(false)
              }}
            >
              <Slot atlasIdx={item.atlasIdx} size="sm" />
              <span className="search-name">{item.name}</span>
              <span className={`mono search-cost${item.uncraftable ? ' cost-infinite' : ''}`}>
                {item.uncraftable ? 'uncraftable' : solveId ? fmtCost(item.cost) : ''}
              </span>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}