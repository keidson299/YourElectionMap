import { describe, expect, it } from 'vitest'
import {
  DESIGN,
  fitFrame,
  groundOf,
  pickScaleBar,
  viewBoxOf,
  type SheetPoint,
} from './frame'

const ADDRESS: SheetPoint = [1127, 629]
const desktop = DESIGN.desktop.span

// State House 44 from the spec: small enough to sit inside the design frame.
const SMALL: SheetPoint[][] = [
  [
    [930, 495],
    [1310, 520],
    [1270, 735],
    [1000, 765],
  ],
]

// Reaches 487 units left and 379 up from the address.
const LARGE: SheetPoint[][] = [
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

describe('pickScaleBar', () => {
  it('picks the round distance closest to the spec’s 92px', () => {
    expect(pickScaleBar(92)).toEqual({ miles: 1, width: 92 })
    expect(pickScaleBar(40)).toEqual({ miles: 2, width: 80 })
    expect(pickScaleBar(400)).toEqual({ miles: 0.25, width: 100 })
  })

  it('goes down to a tenth of a mile for a street-level view', () => {
    expect(pickScaleBar(920)).toEqual({ miles: 0.1, width: 92 })
  })
})

describe('groundOf', () => {
  it('lays paper well past the frame, centred on it', () => {
    const frame = fitFrame(ADDRESS, SMALL, desktop)

    expect(groundOf(frame)).toEqual({
      x: 1127 - 930,
      y: 629 - 744,
      width: 1860,
      height: 1488,
    })
  })

  it('follows the frame wherever it is centred', () => {
    const ground = groundOf(fitFrame([31_000, -10_000], SMALL, desktop))

    expect(ground.x).toBeLessThan(31_000)
    expect(ground.x + ground.width).toBeGreaterThan(31_000)
    expect(ground.y).toBeLessThan(-10_000)
    expect(ground.y + ground.height).toBeGreaterThan(-10_000)
  })
})
