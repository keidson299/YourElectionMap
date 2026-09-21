import { describe, expect, it } from 'vitest'
import { mapUrl, parseRoute } from './routes'

describe('parseRoute', () => {
  it('treats the root as home', () => {
    expect(parseRoute('/')).toEqual({ name: 'home' })
  })

  it('treats an unknown path as home', () => {
    expect(parseRoute('/somewhere-else?address=x')).toEqual({ name: 'home' })
  })

  it('reads an address off the map URL', () => {
    expect(parseRoute('/map?address=1420+Maple+Street%2C+Springfield')).toEqual({
      name: 'map',
      target: { address: '1420 Maple Street, Springfield' },
      district: null,
    })
  })

  it('reads coordinates off the map URL', () => {
    expect(parseRoute('/map?lat=42.101&lon=-71.2')).toEqual({
      name: 'map',
      target: { coords: { lat: 42.101, lon: -71.2 } },
      district: null,
    })
  })

  it('prefers the address when a URL carries both', () => {
    expect(parseRoute('/map?address=Springfield&lat=42.1&lon=-71.2')).toEqual({
      name: 'map',
      target: { address: 'Springfield' },
      district: null,
    })
  })

  it('reads the selected district', () => {
    expect(parseRoute('/map?address=Springfield&district=county-3')).toEqual({
      name: 'map',
      target: { address: 'Springfield' },
      district: 'county-3',
    })
  })

  it('ignores a trailing slash and casing in the path', () => {
    expect(parseRoute('/Map/?address=Springfield')).toEqual({
      name: 'map',
      target: { address: 'Springfield' },
      district: null,
    })
  })

  it('falls back to home when there is nothing to look up', () => {
    expect(parseRoute('/map')).toEqual({ name: 'home' })
    expect(parseRoute('/map?address=%20%20')).toEqual({ name: 'home' })
    expect(parseRoute('/map?district=county-3')).toEqual({ name: 'home' })
  })

  it('rejects coordinates that are not real positions', () => {
    expect(parseRoute('/map?lat=abc&lon=-71.2')).toEqual({ name: 'home' })
    expect(parseRoute('/map?lat=200&lon=-71.2')).toEqual({ name: 'home' })
    expect(parseRoute('/map?lat=42.1')).toEqual({ name: 'home' })
  })
})

describe('mapUrl', () => {
  it('round-trips an address', () => {
    const target = { address: '1420 Maple Street, Springfield' }
    const url = mapUrl(target)

    expect(url).toBe('/map?address=1420+Maple+Street%2C+Springfield')
    expect(parseRoute(url)).toEqual({ name: 'map', target, district: null })
  })

  it('round-trips coordinates, rounded to six places', () => {
    const url = mapUrl({ coords: { lat: 42.10123456789, lon: -71.2 } })

    expect(url).toBe('/map?lat=42.101235&lon=-71.2')
    expect(parseRoute(url)).toEqual({
      name: 'map',
      target: { coords: { lat: 42.101235, lon: -71.2 } },
      district: null,
    })
  })

  it('round-trips a selected district', () => {
    const target = { address: 'Springfield' }
    const url = mapUrl(target, { district: 'state-upper-22' })

    expect(url).toBe('/map?address=Springfield&district=state-upper-22')
    expect(parseRoute(url)).toEqual({
      name: 'map',
      target,
      district: 'state-upper-22',
    })
  })

  it('leaves the district out when there is none', () => {
    expect(mapUrl({ address: 'Springfield' }, { district: null })).toBe(
      '/map?address=Springfield',
    )
  })
})
