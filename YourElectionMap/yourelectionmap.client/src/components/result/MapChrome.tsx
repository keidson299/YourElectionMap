import { LineSwatch } from './LineSwatch'
import { countOf, districtName } from '../../districts/labels'
import type { MapLayers } from '../../districts/selection'
import type { District } from '../../districts/types'
import type { ScaleBar as ScaleBarValue } from '../../map/camera'
import './MapChrome.css'

// The cards and controls that sit over the result map. The map itself is
// hidden from assistive technology, so everything it shows that matters is
// said again here in text.

type ShowingChipProps = {
  district: District
  compact: boolean
  titleId: string
}

export function ShowingChip({ district, compact, titleId }: ShowingChipProps) {
  const meta = compact ? null : district.area

  return (
    <div className="map-chip">
      <p className="map-chip__eyebrow">Showing</p>
      <h2 id={titleId} className="map-chip__title">
        {districtName(district, compact ? 'short' : 'full')}
      </h2>
      {meta === null ? null : <p className="map-chip__meta">{meta}</p>}
    </div>
  )
}

type ZoomControlsProps = {
  canZoomIn: boolean
  canZoomOut: boolean
  onZoomIn: () => void
  onZoomOut: () => void
}

// aria-disabled rather than disabled: a button disabled while it has focus
// drops focus to the page. The zoom is clamped, so a press at a limit is a
// no-op anyway.
export function ZoomControls({
  canZoomIn,
  canZoomOut,
  onZoomIn,
  onZoomOut,
}: ZoomControlsProps) {
  return (
    <div className="map-zoom">
      <button
        type="button"
        className="map-zoom__button"
        aria-label="Zoom in"
        aria-disabled={!canZoomIn || undefined}
        onClick={onZoomIn}
      >
        +
      </button>
      <button
        type="button"
        className="map-zoom__button"
        aria-label="Zoom out"
        aria-disabled={!canZoomOut || undefined}
        onClick={onZoomOut}
      >
        −
      </button>
    </div>
  )
}

export function MapLegend({ layers }: { layers: MapLayers }) {
  return (
    <div className="map-legend">
      <p className="map-legend__title">On this map</p>
      <ul className="map-legend__list">
        <li className="map-legend__item map-legend__item--selected">
          <LineSwatch role="selected" width={24} />
          <span>{districtName(layers.selected, 'legend')} — selected</span>
        </li>
        {layers.dashed === null ? null : (
          <li className="map-legend__item">
            <LineSwatch role="dashed" width={24} />
            <span>{districtName(layers.dashed, 'legend')}</span>
          </li>
        )}
        {layers.dotted === null ? null : (
          <li className="map-legend__item">
            <LineSwatch role="dotted" width={24} />
            <span>{districtName(layers.dotted, 'legend')}</span>
          </li>
        )}
      </ul>
    </div>
  )
}

export function ScaleBar({ scale }: { scale: ScaleBarValue }) {
  return (
    <div className="map-scale">
      <span
        className="map-scale__bar"
        style={{ width: `${scale.width}px` }}
        aria-hidden="true"
      >
        <span className="map-scale__segment map-scale__segment--filled" />
        <span className="map-scale__segment" />
      </span>
      <span className="map-scale__labels" style={{ width: `${scale.width}px` }}>
        <span className="visually-hidden">Scale: </span>
        <span aria-hidden="true">0</span>
        <span>{scale.miles} mi</span>
      </span>
    </div>
  )
}

/** The phone's stand-in for the legend. */
export function BoundaryPill({ count }: { count: number }) {
  return (
    <p className="map-pill">
      <LineSwatch role="selected" width={20} />
      <span>{countOf(count, 'boundary', 'boundaries')}</span>
    </p>
  )
}

/**
 * The address's name beside its pin. Rendered inside the pin's own element,
 * so it moves with the address as the map is dragged.
 */
export function AddressMarker({ label }: { label: string }) {
  return (
    <p className="map-marker">
      <span className="map-marker__dot" aria-hidden="true" />
      <span>{label}</span>
    </p>
  )
}
