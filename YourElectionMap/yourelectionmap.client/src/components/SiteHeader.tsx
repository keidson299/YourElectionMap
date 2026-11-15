import type { MouseEvent } from 'react'
import { AddressForm } from './AddressForm'
import { LayersIcon } from './icons'
import { ThemeToggle } from './ThemeToggle'
import { openLookup } from '../districts/lookups'
import { navigate } from '../routing/history'
import './SiteHeader.css'

// A real link, so middle-click and open-in-new-tab work, that navigates in
// place on a plain left click.
function goHome(event: MouseEvent<HTMLAnchorElement>) {
  const modified = event.metaKey || event.ctrlKey || event.shiftKey || event.altKey

  if (event.defaultPrevented || event.button !== 0 || modified) {
    return
  }

  event.preventDefault()
  navigate('/')
}

export function SiteHeader({ address }: { address: string | null }) {
  return (
    <header className="site-header">
      <a href="/" className="site-header__brand" onClick={goHome}>
        <LayersIcon size={17} className="site-header__mark" />
        <span className="site-header__wordmark">YourElectionMap</span>
      </a>

      {/* Keyed so a new URL reloads the field. */}
      <AddressForm
        key={address ?? ''}
        variant="header"
        submitLabel="Update"
        initialAddress={address ?? ''}
        // Replace, so Back returns Home rather than through every address tried.
        onSubmit={(next) => openLookup({ address: next }, { replace: true })}
      />

      <p className="site-header__note">
        Nonpartisan · Not affiliated with any election authority
      </p>

      <ThemeToggle className="site-header__theme" />
    </header>
  )
}
