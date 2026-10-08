import { AddressForm } from '../components/AddressForm'
import { DataCredit } from '../components/DataCredit'
import { LayersIcon } from '../components/icons'
import { SurveyMap } from '../components/SurveyMap'
import { ThemeToggle } from '../components/ThemeToggle'
import { openLookup } from '../districts/lookups'
import type { Coordinates } from '../routing/routes'
import './HomePage.css'

export function HomePage() {
  // The lookup runs before the page changes, so an address that cannot be found
  // is said here, under the field the visitor typed it into.
  function handleAddress(address: string) {
    return openLookup({ address })
  }

  function handleCoordinates(coords: Coordinates) {
    return openLookup({ coords })
  }

  return (
    <main className="home">
      <SurveyMap />
      <div className="home__veil" aria-hidden="true" />

      <div className="home__content">
        <section className="home__card card">
          <div className="home__intro">
            <p className="home__eyebrow">
              <LayersIcon size={14} />
              <span>Local election maps</span>
            </p>
            <h1 className="home__title" tabIndex={-1}>
              YourElectionMap
            </h1>
            <p className="home__tagline">
              See the district maps you vote in — plus the candidates and dates
              that come with them.
            </p>
          </div>

          <AddressForm
            variant="home"
            submitLabel="Find My Maps"
            onSubmit={handleAddress}
            onLocate={handleCoordinates}
          />

          <p className="home__also">
            <span className="home__also-label">Also included</span>
            <span className="home__also-items">
              candidates · ballot measures · election dates
            </span>
          </p>
        </section>

        <div className="home__meta">
          <div className="home__notes">
            <p className="home__footnote">
              Nonpartisan · Not affiliated with any election authority
            </p>
            <DataCredit className="home__footnote" />
          </div>
          <ThemeToggle />
        </div>
      </div>
    </main>
  )
}
