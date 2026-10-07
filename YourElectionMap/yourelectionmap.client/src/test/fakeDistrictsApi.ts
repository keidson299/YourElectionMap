import { vi } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { resetLookups } from '../districts/lookups'

// Stands in for GET /api/districts, answering with the invented fixture, and can
// be told to fail instead. It is here so the page's flows — waiting, a match, a
// miss, a failure, another go — can be driven from a test.
//
// The statuses are the contract and are mirrored exactly: DistrictsController
// decides the 400s (nothing to look up, an address too long, a point off the
// globe), a 404 is a place with no districts, anything else is a failure.
//
// What decides a match is not: the server geocodes the address and then looks for
// North Carolina districts around the point, where this asks only for a digit and
// a point in the northern hemisphere's western half. Telling a match from a miss
// is all these tests need; what the server really answers is pinned by
// DistrictsEndpointTests.
//
// Responses are the few members the client reads, not real Response objects, so
// nothing here depends on fetch existing in jsdom.

export type FakeDistrictsApi = {
  /** The stubbed global fetch, for counting requests. */
  fetch: ReturnType<typeof vi.fn>
  /** Answer every later request with a 500, as a stopped server would. */
  fail: () => void
  /** Answer properly again. */
  recover: () => void
}

export function startFakeDistrictsApi(): FakeDistrictsApi {
  let failing = false

  const fetchMock = vi.fn((input: string | URL) => {
    if (failing) {
      return Promise.resolve(response(500, {}))
    }

    const query = String(input).split('?')[1] ?? ''

    return Promise.resolve(answer(new URLSearchParams(query)))
  })

  vi.stubGlobal('fetch', fetchMock)

  return {
    fetch: fetchMock,
    fail: () => {
      failing = true
    },
    recover: () => {
      failing = false
    },
  }
}

/** Put the global fetch back, and forget every lookup this test made. */
export function stopFakeDistrictsApi(): void {
  vi.unstubAllGlobals()
  resetLookups()
}

/** The server's cap, from DistrictsController.MaxAddressLength. */
const MAX_ADDRESS_LENGTH = 200

function answer(params: URLSearchParams): Response {
  const typed = params.get('address')?.trim() ?? ''

  if (typed !== '') {
    if (typed.length > MAX_ADDRESS_LENGTH) {
      return response(400, {})
    }

    return /\d/.test(typed)
      ? response(200, sampleLookup({ address: typed }))
      : response(404, {})
  }

  const rawLat = params.get('lat')?.trim() ?? ''
  const rawLon = params.get('lon')?.trim() ?? ''

  // Blank values must not reach Number(), which reads '' as 0.
  if (rawLat === '' || rawLon === '') {
    return response(400, {})
  }

  const lat = Number(rawLat)
  const lon = Number(rawLon)

  if (
    !Number.isFinite(lat) ||
    !Number.isFinite(lon) ||
    Math.abs(lat) > 90 ||
    Math.abs(lon) > 180
  ) {
    return response(400, {})
  }

  // A point on the globe, but only a rough half of it has districts here.
  return lat >= 18 && lat <= 72 && lon >= -180 && lon <= -64
    ? response(200, sampleLookup({ coords: { lat, lon } }))
    : response(404, {})
}

function response(status: number, body: unknown): Response {
  // Through JSON, so the test sees plain arrays and objects off a wire.
  const sent: unknown = JSON.parse(JSON.stringify(body))

  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(sent),
  } as Response
}
