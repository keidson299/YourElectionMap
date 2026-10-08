import { DESIGN, MAP_UNITS_PER_MILE, pickScaleBar, type ScaleBar } from './frame'
import type { LngLat, Polygon } from '../districts/types'

// Where the street map looks and what its scale bar says. Pure, so it can be
// tested without a map: the component hands the results to MapLibre.

/** [[west, south], [east, north]], the order MapLibre's fitBounds takes. */
export type Bounds = [[number, number], [number, number]]

/** How much the opening view shows at least, in miles. */
export type MinimumSpan = { width: number; height: number }

/** Zoom limits: the whole state's width at the far end, a few houses at the near. */
export const MIN_ZOOM = 6
export const MAX_ZOOM = 17

/** The spec's ×1.5 a press, in MapLibre's doubling zoom levels. */
export const ZOOM_STEP = Math.log2(1.5)

/** North Carolina with room around it, so a coastal district can still be framed. */
export const MAX_BOUNDS: Bounds = [
  [-86.5, 32.5],
  [-73.5, 37.8],
]

const FIT_PADDING = 0.08
const MILES_PER_DEGREE_LATITUDE = 69.0
const MILES_PER_DEGREE_LONGITUDE_AT_EQUATOR = 69.17
const METRES_PER_MILE = 1609.344
const EARTH_CIRCUMFERENCE_METRES = 40_075_016.686

/** MapLibre's world is 512 pixels across at zoom 0. */
const WORLD_PIXELS_AT_ZOOM_0 = 512

/**
 * The spec's design spans in miles: 11.3 by 9.0 on desktop, enough for a State
 * House district, and 7.3 by 6.0 on a phone.
 */
export function minimumSpan(compact: boolean): MinimumSpan {
  const span = DESIGN[compact ? 'phone' : 'desktop'].span

  return {
    width: span.width / MAP_UNITS_PER_MILE,
    height: span.height / MAP_UNITS_PER_MILE,
  }
}

/**
 * The view the map opens on and returns to when a district is picked: centred on
 * the address, widened only as far as the boundary needs so every edge of it is
 * in view, and never narrower than the design span.
 */
export function openingBounds(
  location: LngLat,
  boundary: readonly Polygon[],
  minimum: MinimumSpan,
): Bounds {
  const [lng, lat] = location
  let reachLng = 0
  let reachLat = 0

  // Holes lie inside their outer ring, so the outers alone set the reach.
  for (const [outer] of boundary) {
    for (const [x, y] of outer ?? []) {
      reachLng = Math.max(reachLng, Math.abs(x - lng))
      reachLat = Math.max(reachLat, Math.abs(y - lat))
    }
  }

  const milesPerDegreeLng =
    MILES_PER_DEGREE_LONGITUDE_AT_EQUATOR * Math.cos(radians(lat))
  const halfLng = Math.max(
    reachLng * (1 + FIT_PADDING),
    minimum.width / 2 / milesPerDegreeLng,
  )
  const halfLat = Math.max(
    reachLat * (1 + FIT_PADDING),
    minimum.height / 2 / MILES_PER_DEGREE_LATITUDE,
  )

  return [
    [lng - halfLng, lat - halfLat],
    [lng + halfLng, lat + halfLat],
  ]
}

/** The scale bar for a view at this zoom, measured at this latitude. */
export function scaleBarAt(zoom: number, latitude: number): ScaleBar {
  const metresPerPixel =
    (EARTH_CIRCUMFERENCE_METRES * Math.cos(radians(latitude))) /
    (WORLD_PIXELS_AT_ZOOM_0 * 2 ** zoom)

  return pickScaleBar(METRES_PER_MILE / metresPerPixel)
}

function radians(degrees: number): number {
  return (degrees * Math.PI) / 180
}
