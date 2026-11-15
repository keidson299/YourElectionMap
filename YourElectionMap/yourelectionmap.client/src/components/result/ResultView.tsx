import { Suspense, lazy, useMemo, useState } from 'react'
import { DistrictRail } from './DistrictRail'
import { maxBoundsFor } from '../../map/camera'
import { mapLayers, resolveSelection } from '../../districts/selection'
import type { DistrictLookup } from '../../districts/types'
import { useIsPhone } from '../../hooks/useIsPhone'
import './ResultView.css'

// MapLibre is most of the page's weight, so it loads with the first result
// rather than with Home.
const DistrictMap = lazy(() =>
  import('./DistrictMap').then((module) => ({ default: module.DistrictMap })),
)

type ResultViewProps = {
  lookup: DistrictLookup
  /** The district named in the URL; an unknown one falls back. */
  requestedDistrict: string | null
  onSelect: (districtId: string) => void
}

export function ResultView({
  lookup,
  requestedDistrict,
  onSelect,
}: ResultViewProps) {
  const compact = useIsPhone()
  const [sheetExpanded, setSheetExpanded] = useState(false)

  // Memoized: a new layers object rebuilds the map's style.
  const { districts } = lookup
  const maxBounds = useMemo(() => maxBoundsFor(districts), [districts])
  const selected = resolveSelection(districts, requestedDistrict)
  const layers = useMemo(
    () => mapLayers(districts, selected),
    [districts, selected],
  )

  return (
    <main className={sheetExpanded ? 'result result--expanded' : 'result'}>
      <h1 className="visually-hidden" tabIndex={-1}>
        Districts for {lookup.label}
      </h1>

      <DistrictRail
        districts={districts}
        layers={layers}
        election={lookup.election}
        compact={compact}
        sheetExpanded={sheetExpanded}
        onToggleSheet={() => setSheetExpanded((expanded) => !expanded)}
        onSelect={(id) => {
          // Put the drawing back in view after a pick from the expanded sheet.
          setSheetExpanded(false)
          onSelect(id)
        }}
      />

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
