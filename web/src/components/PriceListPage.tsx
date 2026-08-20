import { useEffect, useState } from 'react'
import * as api from '../api'
import { fmtCost } from '../format'
import { useStore } from '../storeContext'
import type { ListResponse } from '../types'
import { Slot } from './Slot'

const PAGE_SIZE = 100

export function PriceListPage() {
  const { results, hideUnreachable, setHideUnreachable, openDetail } = useStore()
  const [page, setPage] = useState(0)
  const [list, setList] = useState<ListResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const solveId = results?.solveId ?? null

  useEffect(() => {
    if (!solveId) {
      return
    }
    let live = true
    api
      .list(solveId, page, PAGE_SIZE, hideUnreachable)
      .then((response) => {
        if (live) {
          setList(response)
          setError(null)
        }
      })
      .catch((caught: unknown) => {
        if (live) {
          setError(caught instanceof Error ? caught.message : String(caught))
        }
      })
    return () => {
      live = false
    }
  }, [solveId, page, hideUnreachable])

  if (!solveId) {
    return (
      <main className="page">
        <p className="hint">The price list needs a solve — press Calculate on the planner first.</p>
      </main>
    )
  }

  const pageCount = list ? Math.max(1, Math.ceil(list.total / PAGE_SIZE)) : 1

  return (
    <main className="page">
      <div className="list-controls">
        <label className="garage-toggle">
          <input
            type="checkbox"
            checked={hideUnreachable}
            onChange={(event) => {
              setHideUnreachable(event.target.checked)
              setPage(0)
            }}
          />
          Hide unreachable
        </label>
        <span className="list-pager mono">
          <button
            type="button"
            className="ghost-button"
            disabled={page === 0}
            onClick={() => setPage(page - 1)}
          >
            ‹
          </button>
          {page + 1} / {pageCount}
          <button
            type="button"
            className="ghost-button"
            disabled={page + 1 >= pageCount}
            onClick={() => setPage(page + 1)}
          >
            ›
          </button>
        </span>
        {list ? <span className="hint">{list.total.toLocaleString('en-US')} items</span> : null}
      </div>
      {error ? <p className="warning-row">{error}</p> : null}
      <ul className="price-list">
        {list?.items.map((item) => (
          <li key={item.itemId}>
            <button type="button" className="price-row" onClick={() => openDetail(item.itemId)}>
              <Slot atlasIdx={item.atlasIdx} size="sm" />
              <span className="search-name">{item.name}</span>
              <span className={`mono search-cost${item.cost === null ? ' cost-infinite' : ''}`}>
                {item.uncraftable ? 'uncraftable' : fmtCost(item.cost)}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </main>
  )
}