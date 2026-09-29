import { vi } from 'vitest'
import { sampleLookup } from './sampleLookup'
import { resetLookups } from '../districts/lookups'

// Stands in for GET /api/districts. It answers from the same invented sample the
// server holds, under the same rules the stub applies — an address needs a house
// number, a point has to be in the country — and can be told to fail instead.
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

function answer(params: URLSearchParams): Response {
  const address = params.get('address')

  if (address !== null && address.trim() !== '') {
    return /\d/.test(address)
      ? response(200, sampleLookup({ address: address.trim() }))
      : response(404, {})
  }

  const lat = Number(params.get('lat'))
  const lon = Number(params.get('lon'))

  if (lat >= 18 && lat <= 72 && lon >= -180 && lon <= -64) {
    return response(200, sampleLookup({ coords: { lat, lon } }))
  }

  return response(404, {})
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
