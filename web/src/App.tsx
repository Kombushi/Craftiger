import { useEffect, useState } from 'react'
import { FactoryPage } from './components/FactoryPage'
import { ItemDetailOverlay } from './components/ItemDetailOverlay'
import { PlannerPage } from './components/PlannerPage'
import { PriceListPage } from './components/PriceListPage'
import { Toasts } from './components/Toasts'
import { TooltipProvider } from './components/Tooltip'
import { WeightsModal } from './components/WeightsModal'
import { useFactory } from './factoryContext'
import { FactoryProvider } from './factoryStore'
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

function TopbarStatus({ route }: { route: string }) {
  const { results, status } = useStore()
  const factory = useFactory()
  if (route === '#/factory') {
    return (
      <span className="topbar-status mono">
        {factory.status.phase === 'solving'
          ? 'solving…'
          : factory.plan
            ? factory.plan.status === 'solved'
              ? `${factory.plan.lines.length} lines · ${Math.round(factory.plan.drawEuT).toLocaleString('en-US')} EU/t`
              : factory.plan.status
            : ''}
      </span>
    )
  }
  return (
    <span className="topbar-status mono">
      {status.phase === 'solving'
        ? 'solving…'
        : results
          ? `${results.pricedItems.toLocaleString('en-US')} priced${results.converged ? '' : ' · not converged'}`
          : ''}
    </span>
  )
}

function Shell() {
  const { meta } = useStore()
  const hash = useHashRoute()
  const [weightsOpen, setWeightsOpen] = useState(false)
  const [sidebarHidden, setSidebarHidden] = usePersistent('gtnhp.sidebarHidden', false)
  const route = hash === '#/list' || hash === '#/factory' ? hash : '#/'

  return (
    <div className="app">
      <header className="topbar">
        {route !== '#/list' ? (
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
          <a className={route === '#/' ? 'nav-active' : ''} href="#/">
            Crafting
          </a>
          <a className={route === '#/factory' ? 'nav-active' : ''} href="#/factory">
            Factory
          </a>
          <a className={route === '#/list' ? 'nav-active' : ''} href="#/list">
            Price list
          </a>
        </nav>
        <TopbarStatus route={route} />
        <button type="button" className="action-button" onClick={() => setWeightsOpen(true)}>
          Weights
        </button>
      </header>
      {route === '#/list' ? (
        <PriceListPage />
      ) : route === '#/factory' ? (
        <FactoryPage sidebarHidden={sidebarHidden} />
      ) : (
        <PlannerPage sidebarHidden={sidebarHidden} />
      )}
      <ItemDetailOverlay />
      {weightsOpen ? <WeightsModal onClose={() => setWeightsOpen(false)} /> : null}
      <Toasts />
    </div>
  )
}

export default function App() {
  return (
    <StoreProvider>
      <FactoryProvider>
        <TooltipProvider>
          <Shell />
        </TooltipProvider>
      </FactoryProvider>
    </StoreProvider>
  )
}
