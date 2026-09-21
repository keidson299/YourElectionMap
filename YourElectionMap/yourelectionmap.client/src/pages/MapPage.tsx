import { LayersIcon } from '../components/icons'
import { SiteHeader } from '../components/SiteHeader'
import { SurveyMap } from '../components/SurveyMap'
import { AppLink } from '../routing/AppLink'
import { HOME_URL, type Coordinates } from '../routing/routes'
import './MapPage.css'

// Provisional. Slice 2 replaces this panel with the district rail, the map
// chrome and the phone bottom sheet from design/canvas4/Main.dc.html; the
// header and the map behind it stay.

type MapPageProps = {
  address: string | null
  coords: Coordinates | null
}

export function MapPage({ address, coords }: MapPageProps) {
  const lookup =
    address ?? (coords === null ? '' : `${coords.lat}, ${coords.lon}`)

  return (
    <div className="map-page">
      <SiteHeader address={address} />

      <main className="map-page__body">
        <SurveyMap variant="detail" />
        <div className="map-page__veil" aria-hidden="true" />

        <section className="map-page__panel card">
          <p className="map-page__eyebrow">
            <LayersIcon size={13} />
            <span>{coords === null ? 'Looking up' : 'Looking up location'}</span>
          </p>
          <h1 className="map-page__lookup">{lookup}</h1>
          <p className="map-page__lede">
            Every district that covers this address belongs here: U.S. House,
            both state chambers, county, city and school board, each drawn on
            the map with the dates and candidates that come with it.
          </p>
          <p className="map-page__note">
            The lookup itself arrives in the next slice.
          </p>
          <AppLink href={HOME_URL} className="map-page__back">
            Start over
          </AppLink>
        </section>
      </main>
    </div>
  )
}
