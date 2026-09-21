import { describe, expect, it } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { orderDistricts } from './selection'
import type { SurveyPoint, SurveyRing } from './types'

// Even-odd ray casting: is the point inside the ring?
function contains(ring: SurveyRing, [px, py]: SurveyPoint): boolean {
  let inside = false

  for (let i = 0, j = ring.length - 1; i < ring.length; j = i, i += 1) {
    const [xi, yi] = ring[i]
    const [xj, yj] = ring[j]

    if (yi > py !== yj > py && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) {
      inside = !inside
    }
  }

  return inside
}

describe('sampleLookup', () => {
  it('names an address by its street line', () => {
    expect(
      sampleLookup({ address: '1420 Maple Street, Springfield' }).label,
    ).toBe('1420 Maple Street')
    expect(sampleLookup({ address: 'Springfield' }).label).toBe('Springfield')
  })

  it('names a located lookup plainly', () => {
    expect(sampleLookup({ coords: { lat: 42.1, lon: -71.2 } }).label).toBe(
      'Your location',
    )
  })

  it('says it is a sample', () => {
    expect(sampleLookup({ address: 'Springfield' }).sample).toBe(true)
  })

  it('lists its districts broad to local already', () => {
    const { districts } = sampleLookup({ address: 'Springfield' })

    expect(orderDistricts(districts)).toEqual(districts)
  })

  // The invented boundaries have to be believable: every district that covers
  // the address must actually contain it, and stay on the drawn sheet.
  it('draws every district around the address', () => {
    const { districts, location } = sampleLookup({ address: 'Springfield' })

    for (const district of districts) {
      expect(contains(district.boundary[0], location), district.id).toBe(true)

      for (const [x, y] of district.boundary.flat()) {
        expect(x >= 0 && x <= 1600 && y >= 0 && y <= 1000, district.id).toBe(
          true,
        )
      }
    }
  })
})
