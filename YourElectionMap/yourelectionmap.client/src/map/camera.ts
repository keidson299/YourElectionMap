import type { District, LngLat, Polygon } from '../districts/types'

/** [[west, south], [east, north]], the order MapLibre takes. */
export type Bounds = [[number, number], [number, number]]

export const MIN_ZOOM = 6
export const MAX_ZOOM = 17

/** The spec's ×1.5 a press, in MapLibre's doubling zoom levels. */
export const ZOOM_STEP = Math.log2(1.5)

/**
 * Degrees the map can be dragged past a lookup's districts. MapLibre won't show
 * more than its limits, so they must be wider than a desktop map at MIN_ZOOM
 * (about 21°), or zooming out stops before the button says so.
 */
const MAX_BOUNDS_PADDING = 10

const FIT_PADDING = 0.08
const MILES_PER_DEGREE_LATITUDE = 69.0
const MILES_PER_DEGREE_LONGITUDE_AT_EQUATOR = 69.17
const METRES_PER_MILE = 1609.344
const EARTH_CIRCUMFERENCE_METRES = 40_075_016.686
/** MapLibre's world is 512 pixels across at zoom 0. */
const WORLD_PIXELS_AT_ZOOM_0 = 512

/** The spec's design frames, in miles. */
export const MINIMUM_SPAN = {
  desktop: { width: 11.3, height: 9.0 },
  phone: { width: 7.3, height: 6.0 },
}

/**
 * Centred on the address, widened only as far as the boundary needs, and never
 * narrower than the design frame.
 */
export function openingBounds(
  location: LngLat,
  boundary: readonly Polygon[],
  compact: boolean,
): Bounds {
  const [lng, lat] = location
  const minimum = MINIMUM_SPAN[compact ? 'phone' : 'desktop']
  let reachLng = 0
  let reachLat = 0

  for (const [x, y] of outerPoints(boundary)) {
    reachLng = Math.max(reachLng, Math.abs(x - lng))
    reachLat = Math.max(reachLat, Math.abs(y - lat))
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

/** The box around every district of the lookup, padded. */
export function maxBoundsFor(districts: readonly District[]): Bounds {
  let west = Infinity
  let south = Infinity
  let east = -Infinity
  let north = -Infinity

  for (const district of districts) {
    for (const [x, y] of outerPoints(district.boundary)) {
      west = Math.min(west, x)
      east = Math.max(east, x)
      south = Math.min(south, y)
      north = Math.max(north, y)
    }
  }

  return [
    [west - MAX_BOUNDS_PADDING, Math.max(-85, south - MAX_BOUNDS_PADDING)],
    [east + MAX_BOUNDS_PADDING, Math.min(85, north + MAX_BOUNDS_PADDING)],
  ]
}

export type ScaleBar = { miles: number; width: number }

// A city block up to fifty miles: a congressional district is a hundred across.
const SCALE_DISTANCES = [0.1, 0.25, 0.5, 1, 2, 5, 10, 25, 50]
const SCALE_TARGET_WIDTH = 92

/** The round distance whose bar at this zoom comes closest to the spec's 92px. */
export function scaleBarAt(zoom: number, latitude: number): ScaleBar {
  const metresPerPixel =
    (EARTH_CIRCUMFERENCE_METRES * Math.cos(radians(latitude))) /
    (WORLD_PIXELS_AT_ZOOM_0 * 2 ** zoom)
  const pixelsPerMile = METRES_PER_MILE / metresPerPixel
  const gap = (miles: number) => Math.abs(miles * pixelsPerMile - SCALE_TARGET_WIDTH)
  const best = SCALE_DISTANCES.reduce((a, b) => (gap(b) < gap(a) ? b : a))

  return { miles: best, width: Math.round(best * pixelsPerMile * 100) / 100 }
}

/** Holes lie inside the outer rings, so the outer rings are all that matter. */
function* outerPoints(boundary: readonly Polygon[]) {
  for (const [outer] of boundary) {
    yield* outer ?? []
  }
}

function radians(degrees: number): number {
  return (degrees * Math.PI) / 180
}
