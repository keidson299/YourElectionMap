import { describe, expect, it } from 'vitest'
import { maxBoundsFor, minimumSpan, openingBounds, scaleBarAt } from './camera'
import type { District, LngLat, Polygon } from '../districts/types'

// Downtown Raleigh, where a degree of longitude is about 56 miles and a degree
// of latitude about 69.
const ADDRESS: LngLat = [-78.64, 35.78]

/** A box around the address, `west` and so on in degrees from it. */
function box(west: number, south: number, east: number, north: number): Polygon[] {
  const [lng, lat] = ADDRESS
  const ring: LngLat[] = [
    [lng - west, lat - south],
    [lng + east, lat - south],
    [lng + east, lat + north],
    [lng - west, lat + north],
    [lng - west, lat - south],
  ]

  return [[ring]]
}

const desktop = minimumSpan(false)

describe('minimumSpan', () => {
  it('is the spec’s design span in miles', () => {
    expect(desktop.width).toBeCloseTo(620 / 55)
    expect(desktop.height).toBeCloseTo(496 / 55)
    expect(minimumSpan(true).width).toBeCloseTo(400 / 55)
  })
})

describe('openingBounds', () => {
  it('keeps the design span when the district already fits', () => {
    const [[west, south], [east, north]] = openingBounds(
      ADDRESS,
      box(0.01, 0.01, 0.01, 0.01),
      desktop,
    )
    const milesAcross = (east - west) * 69.17 * Math.cos((ADDRESS[1] * Math.PI) / 180)
    const milesHigh = (north - south) * 69.0

    expect(milesAcross).toBeCloseTo(desktop.width, 5)
    expect(milesHigh).toBeCloseTo(desktop.height, 5)
  })

  it('widens to take in a large district, with padding, still centred', () => {
    const bounds = openingBounds(ADDRESS, box(1.0, 0.2, 0.4, 0.6), desktop)
    const [[west, south], [east, north]] = bounds

    // The farthest reach on each axis, either side, plus 8%.
    expect(ADDRESS[0] - west).toBeCloseTo(1.08)
    expect(east - ADDRESS[0]).toBeCloseTo(1.08)
    expect(north - ADDRESS[1]).toBeCloseTo(0.648)
    expect(ADDRESS[1] - south).toBeCloseTo(0.648)
  })

  it('measures from the outer rings, which hold any holes', () => {
    const [outer] = box(0.5, 0.5, 0.5, 0.5)[0]
    const hole = box(0.1, 0.1, 0.1, 0.1)[0][0]

    expect(openingBounds(ADDRESS, [[outer, hole]], desktop)).toEqual(
      openingBounds(ADDRESS, [[outer]], desktop),
    )
  })
})

describe('maxBoundsFor', () => {
  function district(boundary: Polygon[]): District {
    return {
      id: 'x',
      level: 'state-lower',
      office: 'State House',
      unit: 'District',
      number: '1',
      detail: 'State',
      area: null,
      population: null,
      boundary,
    }
  }

  it('is the box around every district, two degrees wider each way', () => {
    const [lng, lat] = ADDRESS

    expect(
      maxBoundsFor([
        district(box(0.5, 0.1, 0.1, 0.1)),
        district(box(0.1, 0.3, 0.2, 0.4)),
      ]),
    ).toEqual([
      [expect.closeTo(lng - 2.5), expect.closeTo(lat - 2.3)],
      [expect.closeTo(lng + 2.2), expect.closeTo(lat + 2.4)],
    ])
  })

  it('follows the lookup to another state', () => {
    // The State Capitol, Sacramento: nowhere near North Carolina.
    const [[west], [east]] = maxBoundsFor([
      district([
        [
          [
            [-121.6, 38.5],
            [-121.4, 38.5],
            [-121.4, 38.7],
            [-121.6, 38.5],
          ],
        ],
      ]),
    ])

    expect(west).toBeCloseTo(-123.6)
    expect(east).toBeCloseTo(-119.4)
  })
})

describe('scaleBarAt', () => {
  it('reads a mile at the zoom a State House district opens at', () => {
    expect(scaleBarAt(12, ADDRESS[1]).miles).toBe(1)
  })

  it('reaches a tenth of a mile at street level', () => {
    expect(scaleBarAt(15, ADDRESS[1]).miles).toBe(0.1)
  })

  it('reaches the distances a congressional district is measured in', () => {
    const scale = scaleBarAt(8, ADDRESS[1])

    expect(scale.miles).toBeGreaterThanOrEqual(10)
    expect(scale.width).toBeGreaterThan(40)
    expect(scale.width).toBeLessThan(150)
  })

  it('halves the distance for each zoom level in', () => {
    const far = scaleBarAt(10, ADDRESS[1])
    const near = scaleBarAt(11, ADDRESS[1])

    expect(near.width / near.miles).toBeCloseTo((2 * far.width) / far.miles, 0)
  })
})
