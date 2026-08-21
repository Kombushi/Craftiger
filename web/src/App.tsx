import { useEffect, useState } from 'react'
import { ItemDetailOverlay } from './components/ItemDetailOverlay'
import { PlannerPage } from './components/PlannerPage'
import { PriceListPage } from './components/PriceListPage'
import { Toasts } from './components/Toasts'
import { TooltipProvider } from './components/Tooltip'
import { WeightsModal } from './components/WeightsModal'
import { StoreProvider } from './store'
import { useStore } from './storeContext'
import { usePersistent } from './usePersistent'

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
  const { meta, results, status } = useStore()
  const hash = useHashRoute()
  const [weightsOpen, setWeightsOpen] = useState(false)
  const [sidebarHidden, setSidebarHidden] = usePersistent('gtnhp.sidebarHidden', false)

  return (
    <div className="app">
      <header className="topbar">
        {hash !== '#/list' ? (
          <button
            type="button"
            className="menu-button"
            title={sidebarHidden ? 'Show the sidebar' : 'Hide the sidebar'}
            onClick={() => setSidebarHidden((hidden) => !hidden)}
          >
            ☰
          </button>
        ) : null}
        <span className="brand">
          <img className="brand-logo" src="/favicon.png" alt="" />
          <span className="brand-name">CRAFTIGER</span>
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
      {hash === '#/list' ? <PriceListPage /> : <PlannerPage sidebarHidden={sidebarHidden} />}
      <ItemDetailOverlay />
      {weightsOpen ? <WeightsModal onClose={() => setWeightsOpen(false)} /> : null}
      <Toasts />
    </div>
  )
}

export default function App() {
  return (
    <StoreProvider>
      <TooltipProvider>
        <Shell />
      </TooltipProvider>
    </StoreProvider>
  )
}