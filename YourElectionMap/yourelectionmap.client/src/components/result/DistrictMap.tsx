import { useEffect, useId, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Map as MapLibreMap, Marker, setWorkerUrl } from 'maplibre-gl'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url'
import { LineSwatch } from './LineSwatch'
import { countOf, districtName } from '../../districts/labels'
import type { MapLayers } from '../../districts/selection'
import type { LngLat } from '../../districts/types'
import {
  MAX_ZOOM,
  MIN_ZOOM,
  ZOOM_STEP,
  openingBounds,
  scaleBarAt,
  type Bounds,
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
import './MapChrome.css'

// One MapLibre map for the life of the result page. It opens on the address,
// fitted to the selected boundary, and refits when the selection changes. The
// drawing is hidden from assistive technology, so the chrome over it says in
// text everything it shows.

// MapLibre looks for its worker beside its own file, which bundling breaks;
// ?url makes Vite emit it.
setWorkerUrl(workerUrl)

type DistrictMapProps = {
  location: LngLat
  label: string
  layers: MapLayers
  /** How far the map can be dragged, from maxBoundsFor. */
  maxBounds: Bounds
  /** The phone layout: a tighter frame, a short chip, a pill for the legend. */
  compact: boolean
}

/** The pin's parts in the spec's units, and its width in CSS pixels. */
const PIN = {
  desktop: { dot: 7, ring: 21, arm: 40, pixels: 134 },
  phone: { dot: 6, ring: 16, arm: 32, pixels: 62 },
}

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
  const [view, setView] = useState<{ zoom: number; latitude: number } | null>(null)
  const colors = useMapColors()

  // React draws the pin into an element MapLibre positions.
  const [pinElement] = useState(() => document.createElement('div'))

  const style = useMemo(
    () => buildStyle({ layers, colors, compact }),
    [layers, colors, compact],
  )
  // Memoized: the map re-renders on every frame of a drag, and this reads every
  // point of the boundary.
  const selectedBoundary = layers.selected.boundary
  const bounds = useMemo(
    () => openingBounds(location, selectedBoundary, compact),
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

  // Read by the effect that creates the map, which runs once.
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
      // No WebGL. Only the attempt can tell, and it needs the mounted container.
      // oxlint-disable-next-line react/set-state-in-effect
      setFailed(true)
      return
    }

    created.touchZoomRotate.disableRotation()
    created.keyboard.disableRotation()

    // The hatch is drawn the first time the style asks for it.
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

  const { dot, ring, arm, pixels } = PIN[compact ? 'phone' : 'desktop']
  const chipMeta = compact ? null : layers.selected.area
  const scale = view === null ? null : scaleBarAt(view.zoom, view.latitude)
  const boundaryCount = 1 + (layers.dashed ? 1 : 0) + (layers.dotted ? 1 : 0)

  return (
    <section className="district-map" aria-labelledby={titleId}>
      <div ref={containerRef} className="district-map__canvas" aria-hidden="true" />

      {failed ? (
        <p className="district-map__fallback">
          The map can’t be drawn in this browser. Your districts are still
          listed.
        </p>
      ) : null}

      {createPortal(
        <>
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
          {compact ? null : (
            // Inside the pin's element, so it moves with the address on a drag.
            <p className="map-marker">
              <span className="map-marker__dot" aria-hidden="true" />
              <span>{label}</span>
            </p>
          )}
        </>,
        pinElement,
      )}

      <div className="map-chip">
        <p className="map-chip__eyebrow">Showing</p>
        <h2 id={titleId} className="map-chip__title">
          {districtName(layers.selected, compact ? 'short' : 'full')}
        </h2>
        {chipMeta === null ? null : <p className="map-chip__meta">{chipMeta}</p>}
      </div>

      {map === null || view === null ? null : (
        // aria-disabled rather than disabled: disabling a focused button drops
        // focus to the page. The zoom is clamped, so a press at a limit is a no-op.
        <div className="map-zoom">
          <button
            type="button"
            className="map-zoom__button"
            aria-label="Zoom in"
            aria-disabled={view.zoom >= MAX_ZOOM - 0.001 || undefined}
            onClick={() => map.zoomTo(Math.min(MAX_ZOOM, map.getZoom() + ZOOM_STEP))}
          >
            +
          </button>
          <button
            type="button"
            className="map-zoom__button"
            aria-label="Zoom out"
            aria-disabled={view.zoom <= MIN_ZOOM + 0.001 || undefined}
            onClick={() => map.zoomTo(Math.max(MIN_ZOOM, map.getZoom() - ZOOM_STEP))}
          >
            −
          </button>
        </div>
      )}

      {compact ? (
        <p className="map-pill">
          <LineSwatch role="selected" width={20} />
          <span>{countOf(boundaryCount, 'boundary', 'boundaries')}</span>
        </p>
      ) : (
        <>
          <div className="map-legend">
            <p className="map-legend__title">On this map</p>
            <ul className="map-legend__list">
              <li className="map-legend__item map-legend__item--selected">
                <LineSwatch role="selected" width={24} />
                <span>{districtName(layers.selected, 'legend')} — selected</span>
              </li>
              {(['dashed', 'dotted'] as const).map((role) => {
                const district = layers[role]

                return district === null ? null : (
                  <li key={role} className="map-legend__item">
                    <LineSwatch role={role} width={24} />
                    <span>{districtName(district, 'legend')}</span>
                  </li>
                )
              })}
            </ul>
          </div>

          {scale === null ? null : (
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
          )}
        </>
      )}
    </section>
  )
}

/** The theme's map colours, read again when the toggle or the system changes it. */
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
