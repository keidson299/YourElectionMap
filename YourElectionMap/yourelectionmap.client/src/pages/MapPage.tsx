import { useEffect, useRef } from 'react'
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
  const { state, retry } = useLookup(target)
  const waited = useRef(false)

  // A result that arrives after the status card replaces it with no URL
  // change, so App's focus move never runs. Focus the new heading here, but
  // only if focus fell to the body with the card.
  useEffect(() => {
    if (state.status !== 'found') {
      waited.current = true
      return
    }

    if (waited.current && document.activeElement === document.body) {
      document.querySelector<HTMLElement>('h1')?.focus()
    }

    waited.current = false
  }, [state.status])

  return (
    <div className="map-page">
      <SiteHeader address={'address' in target ? target.address : null} />

      {state.status === 'found' ? (
        // Keyed so zoom and the sheet start fresh for each lookup.
        <ResultView
          key={mapUrl(target)}
          lookup={state.lookup}
          requestedDistrict={district}
          // Replace, so Back leaves the page rather than stepping through picks.
          onSelect={(id) => navigate(mapUrl(target, id), { replace: true })}
        />
      ) : (
        <LookupStatus state={state.status} target={target} onRetry={retry} />
      )}
    </div>
  )
}
