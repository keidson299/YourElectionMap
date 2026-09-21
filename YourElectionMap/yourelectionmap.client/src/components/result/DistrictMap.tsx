import { useId, useState, type CSSProperties } from 'react'
import {
  AddressMarker,
  BoundaryPill,
  MapLegend,
  ScaleBar,
  ShowingChip,
  ZoomControls,
} from './MapChrome'
import { SurveyGround } from '../SurveyGround'
import {
  DETAIL_GROUND,
  MESH_LABELS,
  SURVEY_UNITS_PER_MILE,
} from '../surveyMapGeometry'
import { layerCount, type MapLayers } from '../../districts/selection'
import type { SurveyPoint } from '../../districts/types'
import { useElementSize } from '../../hooks/useElementSize'
import {
  DESIGN,
  MAX_ZOOM,
  MIN_ZOOM,
  clampZoom,
  fitFrame,
  lineScale,
  ringsToPath,
  scaleBarFor,
  viewBoxOf,
  zoomFrame,
} from '../../map/frame'
import './DistrictMap.css'

// The result map: the survey sheet, the selected district drawn solid and
// hatched, its two neighbours dashed and dotted, and the address pin. The view
// is always centred on the address and refits to the selected boundary; the
// parent keys this component on the selection, so zoom starts fresh with each.

type DistrictMapProps = {
  location: SurveyPoint
  label: string
  layers: MapLayers
  /** The phone layout: a tighter frame, a short chip, a pill for the legend. */
  compact: boolean
}

// Dash lengths are set here rather than in the stylesheet because they scale
// with the view, and calc() inside stroke-dasharray is not dependable across
// browsers the way it is for stroke-width.
const DASHES = {
  desktop: { dashed: [9, 6], dotted: [1.5, 6] },
  phone: { dashed: [8, 5], dotted: [1.5, 5] },
}

/** The pin, its ring and its crosshair arms, in survey units at the design frame. */
const PIN = {
  desktop: { dot: 7, ring: 21, arm: 40 },
  phone: { dot: 6, ring: 16, arm: 32 },
}

// The spec hatches the selection with hand-placed lines; a pattern at the same
// angle and roughly the same spacing fits whatever boundary is selected.
const HATCH_ID = 'yem-hatch'
const HATCH_SPACING = 34
const HATCH_ANGLE = -32.5

export function DistrictMap({
  location,
  label,
  layers,
  compact,
}: DistrictMapProps) {
  const titleId = useId()
  const [zoom, setZoom] = useState(0)
  const [mapRef, measured] = useElementSize<HTMLElement>()

  const size = compact ? 'phone' : 'desktop'
  const design = DESIGN[size]
  // Until the element is measured (never, in jsdom) it is taken to be at the
  // spec's size, which makes the first paint match the design frame.
  const map = measured ?? design.map
  const frame = zoomFrame(
    fitFrame(location, layers.selected.boundary, design.span),
    zoom,
  )
  const k = lineScale(frame, map, design)
  const [x, y] = location
  const pin = PIN[size]
  const hatch = HATCH_SPACING * k
  const selectedPath = ringsToPath(layers.selected.boundary)

  const dash = (pattern: readonly number[]) =>
    pattern.map((length) => (length * k).toFixed(2)).join(' ')

  return (
    <section ref={mapRef} className="district-map" aria-labelledby={titleId}>
      <svg
        className="district-map__svg"
        viewBox={viewBoxOf(frame)}
        preserveAspectRatio="xMidYMid meet"
        style={{ '--k': k.toFixed(4) } as CSSProperties}
        aria-hidden="true"
        focusable="false"
      >
        <defs>
          <pattern
            id={HATCH_ID}
            width={hatch}
            height={hatch}
            patternUnits="userSpaceOnUse"
            patternTransform={`rotate(${HATCH_ANGLE})`}
          >
            <path
              className="district-map__hatch-line"
              d={`M0,${hatch / 2} H${hatch}`}
            />
          </pattern>
        </defs>

        <SurveyGround ground={DETAIL_GROUND} />

        <g className="district-map__labels">
          {MESH_LABELS.map((meshLabel) => (
            <text key={meshLabel.text} x={meshLabel.x} y={meshLabel.y}>
              {meshLabel.text}
            </text>
          ))}
        </g>

        {layers.dashed === null ? null : (
          <path
            className="district-map__context"
            d={ringsToPath(layers.dashed.boundary)}
            strokeDasharray={dash(DASHES[size].dashed)}
          />
        )}
        {layers.dotted === null ? null : (
          <path
            className="district-map__context district-map__context--dotted"
            d={ringsToPath(layers.dotted.boundary)}
            strokeDasharray={dash(DASHES[size].dotted)}
          />
        )}

        <path className="district-map__selected" d={selectedPath} />
        <path
          className="district-map__hatch"
          d={selectedPath}
          fill={`url(#${HATCH_ID})`}
        />

        {/* Drawn about the origin and scaled with the view, so the pin keeps
            its size on screen at any zoom. */}
        <g
          className="district-map__pin"
          transform={`translate(${x} ${y}) scale(${k.toFixed(4)})`}
        >
          <path
            className="district-map__crosshair"
            d={`M0,${-pin.arm} V${pin.arm} M${-pin.arm},0 H${pin.arm}`}
          />
          <circle className="district-map__ring" r={pin.ring} />
          <circle className="district-map__dot" r={pin.dot} />
        </g>
      </svg>

      <ShowingChip
        district={layers.selected}
        compact={compact}
        titleId={titleId}
      />

      <ZoomControls
        canZoomIn={zoom < MAX_ZOOM}
        canZoomOut={zoom > MIN_ZOOM}
        onZoomIn={() => setZoom((current) => clampZoom(current + 1))}
        onZoomOut={() => setZoom((current) => clampZoom(current - 1))}
      />

      {compact ? (
        <BoundaryPill count={layerCount(layers)} />
      ) : (
        <>
          <MapLegend layers={layers} />
          <ScaleBar scale={scaleBarFor(frame, map, SURVEY_UNITS_PER_MILE)} />
          <AddressMarker label={label} />
        </>
      )}
    </section>
  )
}
