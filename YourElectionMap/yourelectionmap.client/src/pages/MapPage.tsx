import { LookupStatus } from '../components/result/LookupStatus'
import { ResultView } from '../components/result/ResultView'
import { SiteHeader } from '../components/SiteHeader'
import { useLookup } from '../districts/lookups'
import { navigate } from '../routing/history'
import { mapUrl, type LookupTarget } from '../routing/routes'
import './MapPage.css'

type MapPageProps = {
  target: LookupTarget
  district: string | null
}

export function MapPage({ target, district }: MapPageProps) {
  // Already in hand when the lookup was run by a form; fetched here when a map
  // URL is opened or reloaded.
  const { state, retry } = useLookup(target)

  function handleSelect(districtId: string) {
    // Replace rather than push, so Back leaves the page instead of stepping
    // through every district that was looked at.
    navigate(mapUrl(target, { district: districtId }), { replace: true })
  }

  return (
    <div className="map-page">
      <SiteHeader address={'address' in target ? target.address : null} />

      {state.status === 'found' ? (
        /* Keyed on the lookup so zoom and the sheet start fresh for each one. */
        <ResultView
          key={mapUrl(target)}
          lookup={state.lookup}
          requestedDistrict={district}
          onSelect={handleSelect}
        />
      ) : (
        <LookupStatus state={state.status} target={target} onRetry={retry} />
      )}
    </div>
  )
}
