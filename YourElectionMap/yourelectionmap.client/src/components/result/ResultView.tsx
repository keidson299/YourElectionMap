import { Suspense, lazy, useMemo, useState } from 'react'
import { DistrictRail } from './DistrictRail'
import { maxBoundsFor } from '../../map/camera'
import {
  mapLayers,
  orderDistricts,
  resolveSelection,
} from '../../districts/selection'
import type { DistrictLookup } from '../../districts/types'
import { PHONE_QUERY, useMediaQuery } from '../../hooks/useMediaQuery'
import './ResultView.css'

// The result screen under the header: the rail of districts beside the map on
// desktop, the map over a sheet on a phone. It renders whatever DistrictLookup
// it is given, so where that lookup comes from can change underneath it.

// The street map library is most of the page's weight, so it loads with the
// first result rather than with Home.
const DistrictMap = lazy(() =>
  import('./DistrictMap').then((module) => ({ default: module.DistrictMap })),
)

type ResultViewProps = {
  lookup: DistrictLookup
  /** The district named in the URL, if any; an unknown one falls back. */
  requestedDistrict: string | null
  onSelect: (districtId: string) => void
}

export function ResultView({
  lookup,
  requestedDistrict,
  onSelect,
}: ResultViewProps) {
  const compact = useMediaQuery(PHONE_QUERY)
  const [sheetExpanded, setSheetExpanded] = useState(false)

  // Kept stable between renders: the map rebuilds its drawing only when what
  // these say changes, not whenever the sheet opens or closes.
  const districts = useMemo(
    () => orderDistricts(lookup.districts),
    [lookup.districts],
  )
  const maxBounds = useMemo(() => maxBoundsFor(districts), [districts])
  const selected = resolveSelection(districts, requestedDistrict)
  const layers = useMemo(
    () => (selected === undefined ? null : mapLayers(districts, selected)),
    [districts, selected],
  )
  const heading = (
    <h1 className="visually-hidden" tabIndex={-1}>
      Districts for {lookup.label}
    </h1>
  )

  // A guard only: the data layer turns a lookup with no districts into the
  // no-match state, which the map page shows instead of this view.
  if (layers === null) {
    return (
      <main className="result result--empty">
        {heading}
        <p className="result__empty card">
          No districts were found for this address.
        </p>
      </main>
    )
  }

  function handleSelect(districtId: string) {
    // Put the drawing back in view after a pick from the expanded sheet.
    setSheetExpanded(false)
    onSelect(districtId)
  }

  return (
    <main className={sheetExpanded ? 'result result--expanded' : 'result'}>
      {heading}

      <DistrictRail
        districts={districts}
        layers={layers}
        election={lookup.election}
        source={lookup.source}
        compact={compact}
        sheetExpanded={sheetExpanded}
        onToggleSheet={() => setSheetExpanded((expanded) => !expanded)}
        onSelect={handleSelect}
      />

      {/* One map for the life of the page: a new selection redraws and refits
          it rather than making another. */}
      <Suspense fallback={<section className="district-map" aria-hidden="true" />}>
        <DistrictMap
          location={lookup.location}
          label={lookup.label}
          layers={layers}
          maxBounds={maxBounds}
          compact={compact}
        />
      </Suspense>
    </main>
  )
}
