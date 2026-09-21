import { AddressForm } from './AddressForm'
import { LayersIcon } from './icons'
import { ThemeToggle } from './ThemeToggle'
import { AppLink } from '../routing/AppLink'
import { navigate } from '../routing/history'
import { HOME_URL, mapUrl } from '../routing/routes'
import './SiteHeader.css'

type SiteHeaderProps = {
  address: string | null
}

export function SiteHeader({ address }: SiteHeaderProps) {
  return (
    <header className="site-header">
      <AppLink href={HOME_URL} className="site-header__brand">
        <LayersIcon size={17} className="site-header__mark" />
        <span className="site-header__wordmark">YourElectionMap</span>
      </AppLink>

      {/* Keyed on the address so a new URL reloads the field with it. */}
      <AddressForm
        key={address ?? ''}
        variant="header"
        submitLabel="Update"
        initialAddress={address ?? ''}
        onSubmit={(next) => navigate(mapUrl({ address: next }), { replace: true })}
      />

      <p className="site-header__note">
        Nonpartisan · Not affiliated with any election authority
      </p>

      <ThemeToggle className="site-header__theme" />
    </header>
  )
}
