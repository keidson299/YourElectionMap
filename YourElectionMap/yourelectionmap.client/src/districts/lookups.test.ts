import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { openLookup, problemNotice, requestLookup, resetLookups } from './lookups'
import {
  startFakeDistrictsApi,
  stopFakeDistrictsApi,
  type FakeDistrictsApi,
} from '../test/fakeDistrictsApi'

const MAPLE = { address: '1420 Maple Street, Springfield' } as const

function currentUrl() {
  return `${window.location.pathname}${window.location.search}`
}

describe('lookups', () => {
  let api: FakeDistrictsApi

  beforeEach(() => {
    window.history.replaceState(null, '', '/')
    api = startFakeDistrictsApi()
  })

  afterEach(stopFakeDistrictsApi)

  it('asks the districts endpoint for the target in the URL', async () => {
    await requestLookup(MAPLE)

    expect(api.fetch).toHaveBeenCalledTimes(1)
    expect(String(api.fetch.mock.calls[0][0])).toBe(
      '/api/districts?address=1420+Maple+Street%2C+Springfield',
    )

    await requestLookup({ coords: { lat: 39.8, lon: -89.65 } })

    expect(String(api.fetch.mock.calls[1][0])).toBe(
      '/api/districts?lat=39.8&lon=-89.65',
    )
  })

  it('hands back the districts the endpoint found', async () => {
    const state = await requestLookup(MAPLE)

    expect(state.status).toBe('found')
    expect(state.status === 'found' && state.lookup.label).toBe(
      '1420 Maple Street',
    )
    expect(state.status === 'found' && state.lookup.districts).toHaveLength(6)
  })

  it('reads a 404 as an address that could not be placed', async () => {
    const state = await requestLookup({ address: 'Main Street' })

    expect(state.status).toBe('no-match')
  })

  // Not an error: asking again would send the same rejected query.
  it('reads a 400 the same way, not as a server that is down', async () => {
    const state = await requestLookup({ address: `1 ${'x'.repeat(250)} Road` })

    expect(state.status).toBe('no-match')
  })

  it('reads a lookup with no districts the same way', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ label: 'Nowhere', districts: [] }),
      } as Response),
    )

    expect((await requestLookup({ address: '1 Nowhere Road' })).status).toBe(
      'no-match',
    )
  })

  it('reads a failing server, a page of HTML and a dead connection as errors', async () => {
    api.fail()
    expect((await requestLookup(MAPLE)).status).toBe('error')

    resetLookups()
    vi.stubGlobal('fetch', () =>
      Promise.resolve({
        ok: true,
        status: 200,
        json: () => Promise.reject(new SyntaxError('Unexpected token <')),
      } as Response),
    )
    expect((await requestLookup(MAPLE)).status).toBe('error')

    resetLookups()
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Failed to fetch')))
    expect((await requestLookup(MAPLE)).status).toBe('error')
  })

  it('asks once for a target two callers want at the same time', async () => {
    const [first, second] = await Promise.all([
      requestLookup(MAPLE),
      requestLookup({ address: '1420 Maple Street, Springfield' }),
    ])

    expect(api.fetch).toHaveBeenCalledTimes(1)
    expect(first).toBe(second)
  })

  it('keeps an answer, so a second visit needs no request', async () => {
    await requestLookup(MAPLE)
    await requestLookup(MAPLE)

    expect(api.fetch).toHaveBeenCalledTimes(1)
  })

  it('does not keep a failure, so trying again really tries again', async () => {
    api.fail()
    expect((await requestLookup(MAPLE)).status).toBe('error')

    api.recover()
    expect((await requestLookup(MAPLE)).status).toBe('found')
    expect(api.fetch).toHaveBeenCalledTimes(2)
  })

  it('keeps targets apart', async () => {
    await requestLookup(MAPLE)
    await requestLookup({ address: '88 Harbor Road' })

    expect(api.fetch).toHaveBeenCalledTimes(2)
  })
})

describe('openLookup', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/')
    startFakeDistrictsApi()
  })

  afterEach(stopFakeDistrictsApi)

  it('goes to the map of an address it found', async () => {
    expect(await openLookup(MAPLE)).toBeNull()
    expect(currentUrl()).toBe('/map?address=1420+Maple+Street%2C+Springfield')
  })

  it('stays put and says what to do when there is no match', async () => {
    const notice = await openLookup({ address: 'Main Street' })

    expect(currentUrl()).toBe('/')
    expect(notice).toEqual({
      text:
        'We could not find districts for that address in Arizona, California,'
        + ' Idaho, Montana, North Carolina, Oregon, South Carolina, Utah,'
        + ' Washington or Wyoming. Check it, or try adding your city or ZIP code.',
      field: true,
    })
  })

  it('names the location rather than the address when there is no match', async () => {
    const notice = await openLookup({ coords: { lat: 30, lon: -30 } })

    expect(currentUrl()).toBe('/')
    expect(notice?.text).toContain('at your location')
    // The address field is empty and was not the problem, so it is not flagged.
    expect(notice?.field).toBe(false)
  })
})

describe('problemNotice', () => {
  it('does not flag the field when the lookup itself failed', () => {
    expect(problemNotice('error', MAPLE)).toEqual({
      text: 'The district lookup is not responding. Try again in a moment.',
      field: false,
    })
  })
})
