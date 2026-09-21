import { describe, expect, it } from 'vitest'
import {
  DESIGN,
  MAX_ZOOM,
  MIN_ZOOM,
  clampZoom,
  fitFrame,
  lineScale,
  pickScaleBar,
  ringsToPath,
  scaleBarFor,
  viewBoxOf,
  zoomFrame,
} from './frame'
import type { SurveyPoint, SurveyRing } from '../districts/types'

const ADDRESS: SurveyPoint = [1127, 629]
const design = DESIGN.desktop
const desktop = design.span

// State House 44 from the spec: small enough to sit inside the design frame.
const SMALL: SurveyRing[] = [
  [
    [930, 495],
    [1310, 520],
    [1270, 735],
    [1000, 765],
  ],
]

// Reaches 487 units left and 379 up from the address.
const LARGE: SurveyRing[] = [
  [
    [640, 250],
    [1560, 270],
    [1540, 985],
    [680, 960],
  ],
]

describe('fitFrame', () => {
  it('keeps the design span when the boundary already fits', () => {
    expect(fitFrame(ADDRESS, SMALL, desktop)).toEqual({
      center: ADDRESS,
      width: 620,
      height: 496,
    })
  })

  it('widens to take in a large boundary, with padding, still centred', () => {
    const frame = fitFrame(ADDRESS, LARGE, desktop)

    expect(frame.center).toEqual(ADDRESS)
    expect(frame.width).toBeCloseTo(487 * 2 * 1.08)
    expect(frame.height).toBeCloseTo(379 * 2 * 1.08)
  })

  it('reproduces the spec desktop view box at the design frame', () => {
    expect(viewBoxOf(fitFrame(ADDRESS, SMALL, desktop))).toBe('817 381 620 496')
  })
})

describe('zoom', () => {
  const frame = fitFrame(ADDRESS, SMALL, desktop)

  it('looks closer by half again with each step in', () => {
    expect(zoomFrame(frame, 1)).toEqual({
      center: ADDRESS,
      width: 620 / 1.5,
      height: 496 / 1.5,
    })
  })

  it('stops at its limits', () => {
    expect(clampZoom(MAX_ZOOM + 3)).toBe(MAX_ZOOM)
    expect(clampZoom(MIN_ZOOM - 3)).toBe(MIN_ZOOM)
    expect(zoomFrame(frame, 10)).toEqual(zoomFrame(frame, MAX_ZOOM))
  })

  it('scales line weights with how far the view has widened', () => {
    expect(lineScale(frame, design.map, design)).toBe(1)
    expect(lineScale(zoomFrame(frame, -1), design.map, design)).toBeCloseTo(1.5)
    expect(lineScale(zoomFrame(frame, 1), design.map, design)).toBeCloseTo(
      1 / 1.5,
    )
  })

  it('thins line weights on a map drawn larger than the design', () => {
    const larger = { width: 1560, height: 1248 }

    expect(lineScale(frame, larger, design)).toBeCloseTo(1040 / 1560)
  })
})

describe('scale bar', () => {
  it('reads one mile at the design frame, as the spec draws it', () => {
    const scale = scaleBarFor(fitFrame(ADDRESS, SMALL, desktop), design.map, 55)

    expect(scale.miles).toBe(1)
    expect(scale.width).toBeCloseTo(92, 0)
  })

  it('shortens the distance as the view zooms in', () => {
    const frame = fitFrame(ADDRESS, SMALL, desktop)

    expect(scaleBarFor(zoomFrame(frame, 1), design.map, 55).miles).toBe(0.5)
    expect(scaleBarFor(zoomFrame(frame, 2), design.map, 55).miles).toBe(0.5)
  })

  // A 1366×768 laptop: the short side limits the fit, so a mile is ~64px.
  it('measures against the map at the size it is really drawn', () => {
    const frame = fitFrame(ADDRESS, SMALL, desktop)
    const scale = scaleBarFor(frame, { width: 966, height: 582 }, 55)

    expect(scale.miles).toBe(1)
    expect(scale.width).toBeCloseTo(64.5, 0)
  })

  it('picks the round distance closest to the drawn length', () => {
    expect(pickScaleBar(92)).toEqual({ miles: 1, width: 92 })
    expect(pickScaleBar(40)).toEqual({ miles: 2, width: 80 })
    expect(pickScaleBar(400)).toEqual({ miles: 0.25, width: 100 })
  })
})

describe('ringsToPath', () => {
  it('closes one subpath per ring', () => {
    expect(
      ringsToPath([
        [
          [0, 0],
          [10, 0],
          [10, 10],
        ],
        [
          [2, 2],
          [4, 2],
          [4, 4],
        ],
      ]),
    ).toBe('M0,0 L10,0 L10,10 Z M2,2 L4,2 L4,4 Z')
  })
})
