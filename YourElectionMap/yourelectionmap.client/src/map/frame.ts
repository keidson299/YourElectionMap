// The spec's design frames, and the SVG survey sheet the waiting and no-match
// pages draw: where it looks and how much paper it lays down. Everything here is
// in sheet units, except a map element's size, which is in CSS pixels. The result
// map itself is a street map in degrees (see camera.ts); it borrows the design
// spans from here as the least it ever shows.

/**
 * The sheet's scale. One unit stands for 96 feet, so 55 of them make a mile —
 * which is what makes the spec's 92px scale bar read one mile at the design
 * frame.
 */
export const MAP_UNITS_PER_MILE = 55

/** A point on the sheet, in sheet units, y counting down as SVG does. */
export type SheetPoint = readonly [x: number, y: number]

export type Span = { width: number; height: number }

/** A view onto the sheet: always centred on a point. */
export type Frame = { center: SheetPoint; width: number; height: number }

/**
 * One of the spec's design frames: `span` is how much of the drawing it
 * shows, in map units, and `map` is the map element's size in CSS pixels.
 */
export type Design = { span: Span; map: Span }

/**
 * The 1040×832 desktop map beside the rail, and the 390×320 phone map. The spans
 * are the least the map ever shows: 620 units is 11.3 miles, about right for a
 * State House district, and the result map widens past it for anything bigger.
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

/**
 * Centre on a point and widen past the design span only as far as the given
 * rings need, so every edge of them is in view.
 */
export function fitFrame(
  center: SheetPoint,
  rings: readonly (readonly SheetPoint[])[],
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

/**
 * Paper to lay under a frame: three times its size and centred on it, so the
 * sheet still fills the view whatever shape the window is.
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

export type ScaleBar = { miles: number; width: number }

// From a city block, at the street map's closest, up to fifty miles, because a
// congressional district is a hundred miles across.
const SCALE_DISTANCES = [0.1, 0.25, 0.5, 1, 2, 5, 10, 25, 50]
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

function round(value: number): number {
  return Math.round(value * 100) / 100
}
