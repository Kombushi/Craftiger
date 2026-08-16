import { useEffect, useState } from 'react'
import { ItemDetailOverlay } from './components/ItemDetailOverlay'
import { PlannerPage } from './components/PlannerPage'
import { PriceListPage } from './components/PriceListPage'
import { WeightsModal } from './components/WeightsModal'
import { StoreProvider } from './store'
import { useStore } from './storeContext'

function useHashRoute(): string {
  const [hash, setHash] = useState(window.location.hash)
  useEffect(() => {
    const onChange = () => setHash(window.location.hash)
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  return hash
}

function Shell() {
  const { meta, metaError, results, status } = useStore()
  const hash = useHashRoute()
  const [weightsOpen, setWeightsOpen] = useState(false)

  return (
    <div className="app">
      <header className="topbar">
        <span className="brand">
          CRAFTIGER
          {meta ? <span className="brand-pack mono">{meta.packVersion}</span> : null}
        </span>
        <nav className="nav">
          <a className={hash !== '#/list' ? 'nav-active' : ''} href="#/">
            Planner
          </a>
          <a className={hash === '#/list' ? 'nav-active' : ''} href="#/list">
            Price list
          </a>
        </nav>
        <span className="topbar-status mono">
          {status.phase === 'solving'
            ? 'solving…'
            : results
              ? `${results.pricedItems.toLocaleString('en-US')} priced${results.converged ? '' : ' · not converged'}`
              : ''}
        </span>
        <button type="button" className="action-button" onClick={() => setWeightsOpen(true)}>
          Weights
        </button>
      </header>
      {metaError ? (
        <p className="warning-row">The planner API is not reachable: {metaError}</p>
      ) : null}
      {hash === '#/list' ? <PriceListPage /> : <PlannerPage />}
      <ItemDetailOverlay />
      {weightsOpen ? <WeightsModal onClose={() => setWeightsOpen(false)} /> : null}
    </div>
  )
}

export default function App() {
  return (
    <StoreProvider>
      <Shell />
    </StoreProvider>
  )
}