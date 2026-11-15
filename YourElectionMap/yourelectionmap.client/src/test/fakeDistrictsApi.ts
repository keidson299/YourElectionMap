import { vi } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { resetLookups } from '../districts/lookups'

// Stands in for GET /api/districts with the sample fixture. An address with a
// digit, or a point roughly in North America, is a match; anything else is a
// 404. DistrictsEndpointTests pins what the server really answers.

export type FakeDistrictsApi = {
  /** The stubbed global fetch, for counting requests. */
  fetch: ReturnType<typeof vi.fn>
  /** Answer every later request with a 500, as a stopped server would. */
  fail: () => void
  recover: () => void
}

export function startFakeDistrictsApi(): FakeDistrictsApi {
  let failing = false

  const fetchMock = vi.fn((input: string | URL) => {
    const params = new URLSearchParams(String(input).split('?')[1] ?? '')
    return Promise.resolve(failing ? response(500, {}) : answer(params))
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

function answer(params: URLSearchParams): Response {
  const address = params.get('address')

  if (address !== null) {
    return /\d/.test(address)
      ? response(200, sampleLookup({ address }))
      : response(404, {})
  }

  const lat = Number(params.get('lat'))
  const lon = Number(params.get('lon'))

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
