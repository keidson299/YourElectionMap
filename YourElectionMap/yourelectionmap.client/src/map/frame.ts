import type { SurveyPoint, SurveyRing } from '../districts/types'

// Where the result map looks, how far it zooms, and what its scale bar says.
// Everything is in survey units, the drawing's coordinate space.

export type Span = { width: number; height: number }

/** A view onto the drawing: always centred on the address. */
export type Frame = { center: SurveyPoint; width: number; height: number }

/**
 * The spans that reproduce the spec's scale at its two design frames: the
 * 1040×832 desktop map beside the rail, and the 390×320 phone map.
 */
export const DESIGN_SPANS = {
  desktop: { width: 620, height: 496 },
  phone: { width: 400, height: 328 },
} satisfies Record<string, Span>

/**
 * The desktop map at the design frame. The scale bar assumes this size until
 * slice 5 measures the real element.
 */
const DESIGN_MAP_SIZE = { width: 1040, height: 832 }

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

/**
 * How much wider than the design span the view has become. Line weights are
 * multiplied by this so they stay the width the spec draws them on screen
 * while the geometry grows and shrinks.
 */
export function lineScale(frame: Frame, design: Span): number {
  return Math.max(frame.width / design.width, frame.height / design.height)
}

export type ScaleBar = { miles: number; width: number }

const SCALE_DISTANCES = [0.25, 0.5, 1, 2, 5]
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

export function scaleBarFor(frame: Frame, unitsPerMile: number): ScaleBar {
  const pixelsPerUnit = Math.min(
    DESIGN_MAP_SIZE.width / frame.width,
    DESIGN_MAP_SIZE.height / frame.height,
  )

  return pickScaleBar(pixelsPerUnit * unitsPerMile)
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
