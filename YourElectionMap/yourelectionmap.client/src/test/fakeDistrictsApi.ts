import { vi } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { resetLookups } from '../districts/lookups'

// Stands in for GET /api/districts with the invented fixture, and can be told to
// fail, so tests can drive the page's flows: waiting, a match, a miss, a failure.
//
// Statuses mirror DistrictsController exactly: 400 for a query it cannot use, 404
// for no districts, anything else a failure. What counts as a match does not: here
// it is an address with a digit, or a point roughly in North America.
// DistrictsEndpointTests pins what the server really answers.
//
// Responses carry only the members the client reads, so nothing depends on jsdom
// having fetch.

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

  // Roughly North America.
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
