import { useState } from 'react'
import { DistrictMap } from './DistrictMap'
import { DistrictRail } from './DistrictRail'
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

  const districts = orderDistricts(lookup.districts)
  const selected = resolveSelection(districts, requestedDistrict)
  const heading = (
    <h1 className="visually-hidden" tabIndex={-1}>
      Districts for {lookup.label}
    </h1>
  )

  // A guard only: the data layer turns a lookup with no districts into the
  // no-match state, which the map page shows instead of this view.
  if (selected === undefined) {
    return (
      <main className="result result--empty">
        {heading}
        <p className="result__empty card">
          No districts were found for this address.
        </p>
      </main>
    )
  }

  const layers = mapLayers(districts, selected)

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

      <DistrictMap
        key={selected.id}
        location={lookup.location}
        label={lookup.label}
        layers={layers}
        compact={compact}
      />
    </main>
  )
}
