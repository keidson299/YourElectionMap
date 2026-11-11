import type { District, LngLat, Polygon } from '../districts/types'

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

/**
 * How far past a lookup's districts the map can be dragged, in degrees. MapLibre
 * will not show more than the limits, so they have to be wider than a desktop
 * map at MIN_ZOOM (about 21°), or zooming out stops before the button says so.
 */
const MAX_BOUNDS_PADDING = 10

const FIT_PADDING = 0.08
const MILES_PER_DEGREE_LATITUDE = 69.0
const MILES_PER_DEGREE_LONGITUDE_AT_EQUATOR = 69.17
const METRES_PER_MILE = 1609.344
const EARTH_CIRCUMFERENCE_METRES = 40_075_016.686

/** MapLibre's world is 512 pixels across at zoom 0. */
const WORLD_PIXELS_AT_ZOOM_0 = 512

/** The spec's design frames in miles; the desktop one fits a State House district. */
const MINIMUM_SPAN: Record<'desktop' | 'phone', MinimumSpan> = {
  desktop: { width: 11.3, height: 9.0 },
  phone: { width: 7.3, height: 6.0 },
}

export function minimumSpan(compact: boolean): MinimumSpan {
  return MINIMUM_SPAN[compact ? 'phone' : 'desktop']
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

/**
 * How far the map can wander: the box around every district of the lookup, with
 * room around it so a coastal district can still be framed. It follows the
 * lookup, so a California address is not held in North Carolina.
 */
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

/** The scale bar for a view at this zoom, measured at this latitude. */
export function scaleBarAt(zoom: number, latitude: number): ScaleBar {
  const metresPerPixel =
    (EARTH_CIRCUMFERENCE_METRES * Math.cos(radians(latitude))) /
    (WORLD_PIXELS_AT_ZOOM_0 * 2 ** zoom)

  return pickScaleBar(METRES_PER_MILE / metresPerPixel)
}

export type ScaleBar = { miles: number; width: number }

// From a city block, at the street map's closest, up to fifty miles, because a
// congressional district is a hundred miles across.
const SCALE_DISTANCES = [0.1, 0.25, 0.5, 1, 2, 5, 10, 25, 50]
const SCALE_TARGET_WIDTH = 92

/** The round distance whose bar comes closest to the spec's 92px. */
function pickScaleBar(pixelsPerMile: number): ScaleBar {
  let best = SCALE_DISTANCES[0]

  for (const miles of SCALE_DISTANCES) {
    const gap = Math.abs(miles * pixelsPerMile - SCALE_TARGET_WIDTH)
    const bestGap = Math.abs(best * pixelsPerMile - SCALE_TARGET_WIDTH)

    if (gap < bestGap) {
      best = miles
    }
  }

  return { miles: best, width: Math.round(best * pixelsPerMile * 100) / 100 }
}

/** Every point of every outer ring: holes lie inside them, so add no reach. */
function* outerPoints(boundary: readonly Polygon[]) {
  for (const [outer] of boundary) {
    yield* outer ?? []
  }
}

function radians(degrees: number): number {
  return (degrees * Math.PI) / 180
}
