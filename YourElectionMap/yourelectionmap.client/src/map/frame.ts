import type { SurveyPoint, SurveyRing } from '../districts/types'

// Where the result map looks, how far it zooms, and what its scale bar says.
// Everything is in map units, the coordinate space boundaries arrive in, except
// a map element's size, which is in CSS pixels.

/**
 * The map's scale. One unit is 96 feet, so 55 of them make a mile — which is
 * what makes the spec's 92px scale bar read one mile at the design frame. Must
 * agree with the server's `StatePlane.FeetPerMapUnit`.
 */
export const MAP_UNITS_PER_MILE = 55

export type Span = { width: number; height: number }

/** A view onto the drawing: always centred on the address. */
export type Frame = { center: SurveyPoint; width: number; height: number }

/**
 * One of the spec's design frames: `span` is how much of the drawing it
 * shows, in map units, and `map` is the map element's size in CSS pixels.
 */
export type Design = { span: Span; map: Span }

/**
 * The 1040×832 desktop map beside the rail, and the 390×320 phone map. The spans
 * are the least the map ever shows: 620 units is 11.3 miles, about right for a
 * State House district, and `fitFrame` widens past it for anything bigger.
 */
export const DESIGN = {
  desktop: {
    span: { width: 620, height: 496 },
    map: { width: 1040, height: 832 },
  },
  phone: {
    span: { width: 400, height: 328 },
    map: { width: 390, height: 320 },
  },
} satisfies Record<string, Design>

const FIT_PADDING = 0.08

export const MIN_ZOOM = -1
export const MAX_ZOOM = 2
const ZOOM_STEP = 1.5

/**
 * Centre on the address and widen past the design span only as far as the
 * selected boundary needs, so every edge of it is in view.
 */
export function fitFrame(
  center: SurveyPoint,
  rings: readonly SurveyRing[],
  minimum: Span,
): Frame {
  const [cx, cy] = center
  let reachX = 0
  let reachY = 0

  for (const ring of rings) {
    for (const [x, y] of ring) {
      reachX = Math.max(reachX, Math.abs(x - cx))
      reachY = Math.max(reachY, Math.abs(y - cy))
    }
  }

  return {
    center,
    width: Math.max(minimum.width, reachX * 2 * (1 + FIT_PADDING)),
    height: Math.max(minimum.height, reachY * 2 * (1 + FIT_PADDING)),
  }
}

export function clampZoom(zoom: number): number {
  return Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom))
}

/** Positive zoom looks closer; each step is ×1.5. */
export function zoomFrame(frame: Frame, zoom: number): Frame {
  const factor = ZOOM_STEP ** clampZoom(zoom)

  return {
    center: frame.center,
    width: frame.width / factor,
    height: frame.height / factor,
  }
}

/**
 * Paper to lay under a frame: three times its size and centred on it, so the
 * sheet still fills the view after a zoom out, wherever in the state it is.
 */
export function groundOf(frame: Frame): {
  x: number
  y: number
  width: number
  height: number
} {
  const [cx, cy] = frame.center
  const width = frame.width * 3
  const height = frame.height * 3

  return { x: cx - width / 2, y: cy - height / 2, width, height }
}

export function viewBoxOf(frame: Frame): string {
  const [cx, cy] = frame.center

  return [
    cx - frame.width / 2,
    cy - frame.height / 2,
    frame.width,
    frame.height,
  ]
    .map(round)
    .join(' ')
}

/** CSS pixels per map unit when `span` is fitted into `map` ('meet'). */
export function pixelsPerUnit(span: Span, map: Span): number {
  return Math.min(map.width / span.width, map.height / span.height)
}

/**
 * How many times smaller the drawing appears on screen than at the design
 * frame. Line weights and the pin are multiplied by this, so they stay the
 * size the spec draws them whatever the zoom, the fitted boundary or the size
 * of the window.
 */
export function lineScale(frame: Frame, map: Span, design: Design): number {
  return pixelsPerUnit(design.span, design.map) / pixelsPerUnit(frame, map)
}

export type ScaleBar = { miles: number; width: number }

// Up to fifty miles, because a congressional district is a hundred miles across
// and the bar would otherwise be a stub at the widest frames.
const SCALE_DISTANCES = [0.25, 0.5, 1, 2, 5, 10, 25, 50]
const SCALE_TARGET_WIDTH = 92

/** The round distance whose bar comes closest to the spec's 92px. */
export function pickScaleBar(pixelsPerMile: number): ScaleBar {
  let best = SCALE_DISTANCES[0]

  for (const miles of SCALE_DISTANCES) {
    const gap = Math.abs(miles * pixelsPerMile - SCALE_TARGET_WIDTH)
    const bestGap = Math.abs(best * pixelsPerMile - SCALE_TARGET_WIDTH)

    if (gap < bestGap) {
      best = miles
    }
  }

  return { miles: best, width: round(best * pixelsPerMile) }
}

export function scaleBarFor(
  frame: Frame,
  map: Span,
  unitsPerMile: number,
): ScaleBar {
  return pickScaleBar(pixelsPerUnit(frame, map) * unitsPerMile)
}

/** A boundary as SVG path data, one closed subpath per ring. */
export function ringsToPath(rings: readonly SurveyRing[]): string {
  return rings
    .map((ring) => `M${ring.map(([x, y]) => `${x},${y}`).join(' L')} Z`)
    .join(' ')
}

function round(value: number): number {
  return Math.round(value * 100) / 100
}
