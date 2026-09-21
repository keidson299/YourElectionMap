import { ResultView } from '../components/result/ResultView'
import { SiteHeader } from '../components/SiteHeader'
import { sampleLookup } from '../districts/sampleLookup'
import { navigate } from '../routing/history'
import { mapUrl, type LookupTarget } from '../routing/routes'
import './MapPage.css'

type MapPageProps = {
  target: LookupTarget
  district: string | null
}

export function MapPage({ target, district }: MapPageProps) {
  // Invented for now. Slice 3 fetches the lookup from the districts API.
  const lookup = sampleLookup(target)

  function handleSelect(districtId: string) {
    // Replace rather than push, so Back leaves the page instead of stepping
    // through every district that was looked at.
    navigate(mapUrl(target, { district: districtId }), { replace: true })
  }

  return (
    <div className="map-page">
      <SiteHeader address={'address' in target ? target.address : null} />
      {/* Keyed on the lookup so zoom and the sheet start fresh for each one. */}
      <ResultView
        key={mapUrl(target)}
        lookup={lookup}
        requestedDistrict={district}
        onSelect={handleSelect}
      />
    </div>
  )
}
