import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Map as MapLibreMap, Marker, setWorkerUrl } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url'
import {
  AddressMarker,
  BoundaryPill,
  MapLegend,
  ScaleBar,
  ShowingChip,
  ZoomControls,
} from './MapChrome'
import { layerCount, type MapLayers } from '../../districts/selection'
import type { LngLat } from '../../districts/types'
import type { Bounds } from '../../map/camera'
import {
  MAX_ZOOM,
  MIN_ZOOM,
  ZOOM_STEP,
  minimumSpan,
  openingBounds,
  scaleBarAt,
} from '../../map/camera'
import { drawHatch } from '../../map/hatch'
import {
  buildStyle,
  readHatchId,
  readMapColors,
  type MapColors,
  type StyleSpecification,
} from '../../map/style'
import './DistrictMap.css'

// The result map: real streets, towns and water, the selected district solid
// and hatched over them, its two neighbours dashed and dotted, and the address
// pin. It opens centred on the address, fitted to the selected boundary, and
// refits whenever the selection changes; in between, the visitor can drag and
// zoom freely.
//
// One MapLibre map lives for as long as the result page does. Everything it
// draws comes from buildStyle, so a new selection or theme is one setStyle,
// which MapLibre diffs rather than reloads.

// MapLibre looks for its web worker beside its own file, which bundling breaks:
// the worker is never emitted and only the background draws. Importing it with
// ?url makes Vite emit it and gives its URL.
setWorkerUrl(workerUrl)

type DistrictMapProps = {
  location: LngLat
  label: string
  layers: MapLayers
  /** How far the map can be dragged: around the lookup, from maxBoundsFor. */
  maxBounds: Bounds
  /** The phone layout: a tighter frame, a short chip, a pill for the legend. */
  compact: boolean
}

type View = { zoom: number; latitude: number }

/**
 * The pin's dot, ring and crosshair arms in the spec's own units, and how many
 * CSS pixels across it is drawn, as at the spec's design frame.
 */
const PIN = {
  desktop: { dot: 7, ring: 21, arm: 40, pixels: 134 },
  phone: { dot: 6, ring: 16, arm: 32, pixels: 62 },
}

/** How long a refit takes when the selection changes. */
const REFIT_MS = 450

export function DistrictMap({
  location,
  label,
  layers,
  maxBounds,
  compact,
}: DistrictMapProps) {
  const titleId = useId()
  const containerRef = useRef<HTMLDivElement>(null)
  const [map, setMap] = useState<MapLibreMap | null>(null)
  const [failed, setFailed] = useState(false)
  const [view, setView] = useState<View | null>(null)
  const colors = useMapColors()

  // The pin is drawn by React into an element MapLibre positions.
  const [pinElement] = useState(() => document.createElement('div'))

  const style = useMemo(
    () => buildStyle({ layers, colors, compact }),
    [layers, colors, compact],
  )
  // Memoized: the map re-renders on every frame of a drag, and this reads every
  // point of the boundary.
  const selectedBoundary = layers.selected.boundary
  const bounds = useMemo(
    () => openingBounds(location, selectedBoundary, minimumSpan(compact)),
    [location, selectedBoundary, compact],
  )

  // What the map was last given, so a re-render that changes nothing it shows
  // does not touch it, and the first render's values are not applied twice.
  const applied = useRef<{
    style: StyleSpecification
    fit: string
    limits: string
  } | null>(null)
  const fitKey = `${layers.selected.id}|${location.join(',')}|${compact}`
  const limitsKey = maxBounds.flat().join(',')

  // Read through a ref by the effect that creates the map, which runs once.
  const initial = useRef({ style, bounds, fitKey, maxBounds, limitsKey })

  useEffect(() => {
    const container = containerRef.current

    if (container === null) {
      return
    }

    let created: MapLibreMap

    try {
      created = new MapLibreMap({
        container,
        style: initial.current.style,
        bounds: initial.current.bounds,
        minZoom: MIN_ZOOM,
        maxZoom: MAX_ZOOM,
        maxBounds: initial.current.maxBounds,
        dragRotate: false,
        pitchWithRotate: false,
        touchPitch: false,
        attributionControl: { compact: true },
      })
    } catch {
      // No WebGL: MapLibre cannot draw at all. The rail still lists everything.
      // Only the attempt can tell, and the attempt needs the mounted container.
      // oxlint-disable-next-line react/set-state-in-effect
      setFailed(true)
      return
    }

    created.touchZoomRotate.disableRotation()
    created.keyboard.disableRotation()

    // The hatch is drawn on demand, the first time the style asks for it in a
    // given colour and spacing.
    created.on('styleimagemissing', (event: { id: string }) => {
      const hatch = readHatchId(event.id)
      const drawn = hatch && drawHatch(hatch.spacing, hatch.color)

      if (drawn && !created.hasImage(event.id)) {
        created.addImage(event.id, drawn.image, {
          pixelRatio: drawn.pixelRatio,
        })
      }
    })

    const track = () =>
      setView({ zoom: created.getZoom(), latitude: created.getCenter().lat })

    created.on('move', track)
    track()

    applied.current = {
      style: initial.current.style,
      fit: initial.current.fitKey,
      limits: initial.current.limitsKey,
    }
    setMap(created)

    return () => {
      created.remove()
      applied.current = null
      setMap(null)
    }
  }, [])

  useEffect(() => {
    if (map === null || applied.current === null) {
      return
    }

    if (applied.current.style !== style) {
      applied.current.style = style
      map.setStyle(style)
    }

    // Before the refit, so a lookup in another state is not fitted inside the
    // last one's limits.
    if (applied.current.limits !== limitsKey) {
      applied.current.limits = limitsKey
      map.setMaxBounds(maxBounds)
    }

    if (applied.current.fit !== fitKey) {
      applied.current.fit = fitKey
      map.fitBounds(bounds, { duration: REFIT_MS })
    }
  }, [map, style, fitKey, bounds, limitsKey, maxBounds])

  useEffect(() => {
    if (map === null) {
      return
    }

    const marker = new Marker({ element: pinElement, anchor: 'center' })
      .setLngLat([location[0], location[1]])
      .addTo(map)

    return () => {
      marker.remove()
    }
  }, [map, pinElement, location])

  const size = compact ? 'phone' : 'desktop'

  return (
    <section className="district-map" aria-labelledby={titleId}>
      {/* The drawing says nothing a screen reader needs: the chip, the legend
          and the rail say it in text. */}
      <div ref={containerRef} className="district-map__canvas" aria-hidden="true" />

      {failed ? (
        <p className="district-map__fallback">
          The map can’t be drawn in this browser. Your districts are still
          listed.
        </p>
      ) : null}

      {createPortal(
        <>
          <Pin size={size} />
          {compact ? null : <AddressMarker label={label} />}
        </>,
        pinElement,
      )}

      <ShowingChip
        district={layers.selected}
        compact={compact}
        titleId={titleId}
      />

      {map === null || view === null ? null : (
        <ZoomControls
          canZoomIn={view.zoom < MAX_ZOOM - 0.001}
          canZoomOut={view.zoom > MIN_ZOOM + 0.001}
          onZoomIn={() =>
            map.zoomTo(Math.min(MAX_ZOOM, map.getZoom() + ZOOM_STEP))
          }
          onZoomOut={() =>
            map.zoomTo(Math.max(MIN_ZOOM, map.getZoom() - ZOOM_STEP))
          }
        />
      )}

      {compact ? (
        <BoundaryPill count={layerCount(layers)} />
      ) : (
        <>
          <MapLegend layers={layers} />
          {view === null ? null : (
            <ScaleBar scale={scaleBarAt(view.zoom, view.latitude)} />
          )}
        </>
      )}
    </section>
  )
}

/**
 * The pin as the spec draws it: a dot, a dashed ring and a crosshair, at the
 * size they have at the design frame, whatever the zoom.
 */
function Pin({ size }: { size: 'desktop' | 'phone' }) {
  const { dot, ring, arm, pixels } = PIN[size]

  return (
    <svg
      className="district-map__pin"
      width={pixels}
      height={pixels}
      viewBox={`${-arm} ${-arm} ${2 * arm} ${2 * arm}`}
      aria-hidden="true"
      focusable="false"
    >
      <path
        className="district-map__crosshair"
        d={`M0,${-arm} V${arm} M${-arm},0 H${arm}`}
      />
      <circle className="district-map__ring" r={ring} />
      <circle className="district-map__dot" r={dot} />
    </svg>
  )
}

/**
 * The theme's map colours, read again whenever the theme changes: by the
 * toggle, which sets data-theme on <html>, or by the system, when the page
 * follows it.
 */
function useMapColors(): MapColors {
  const [colors, setColors] = useState(readMapColors)

  useEffect(() => {
    const reread = () => setColors(readMapColors())

    const observer = new MutationObserver(reread)
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme'],
    })

    const scheme = window.matchMedia?.('(prefers-color-scheme: dark)')
    scheme?.addEventListener('change', reread)

    return () => {
      observer.disconnect()
      scheme?.removeEventListener('change', reread)
    }
  }, [])

  return colors
}
