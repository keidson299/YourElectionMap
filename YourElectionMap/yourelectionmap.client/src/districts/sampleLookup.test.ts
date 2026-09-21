import { describe, expect, it } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { orderDistricts } from './selection'

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
})
