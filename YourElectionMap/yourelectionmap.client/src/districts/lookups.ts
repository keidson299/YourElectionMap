import { useCallback, useEffect, useSyncExternalStore } from 'react'
import type { DistrictLookup } from './types'
import { getSnapshot, navigate } from '../routing/history'
import { mapUrl, targetQuery, type LookupTarget } from '../routing/routes'

// Every lookup this visit has made, kept outside React so a lookup run on Home
// is still in hand when the map page renders.

export type LookupState =
  | { status: 'loading' }
  | { status: 'found'; lookup: DistrictLookup }
  | { status: 'no-match' }
  | { status: 'error' }

type SettledLookup = Exclude<LookupState, { status: 'loading' }>

export type LookupProblem = 'no-match' | 'error'

/** What to say under the address field; `field` flags the input as the problem. */
export type LookupNotice = { text: string; field: boolean }

const LOADING: LookupState = { status: 'loading' }
const TIMEOUT_MS = 15000

// Keyed by the target's query string, which is also what the request sends.
const entries = new Map<
  string,
  { state: LookupState; answer: Promise<SettledLookup> }
>()
const listeners = new Set<() => void>()

/** Reuses an answer already here or on its way; a failure is tried again. */
export function requestLookup(target: LookupTarget): Promise<SettledLookup> {
  return request(targetQuery(target))
}

/**
 * Looks the target up and goes to its map if found. Resolves to the notice for
 * the address field, or null once the page has moved on.
 */
export async function openLookup(
  target: LookupTarget,
  options: { replace?: boolean } = {},
): Promise<LookupNotice | null> {
  const startedAt = getSnapshot()
  const state = await requestLookup(target)

  // The visitor went elsewhere while waiting: don't pull them back.
  if (getSnapshot() !== startedAt) {
    return null
  }

  if (state.status === 'found') {
    navigate(mapUrl(target), options)
    return null
  }

  return problemNotice(state.status, target)
}

export function useLookup(target: LookupTarget) {
  const query = targetQuery(target)
  const state = useSyncExternalStore(
    subscribe,
    () => entries.get(query)?.state ?? LOADING,
  )
  const retry = useCallback(() => void request(query), [query])

  useEffect(() => {
    void request(query)
  }, [query])

  return { state, retry }
}

export const COVERED_STATES = 'the fifty states'

export function problemNotice(
  problem: LookupProblem,
  target: LookupTarget,
): LookupNotice {
  if (problem === 'error') {
    return {
      text: 'The district lookup is not responding. Try again in a moment.',
      field: false,
    }
  }

  const typed = 'address' in target

  return {
    text: typed
      ? `We could not find districts for that address in ${COVERED_STATES}. Check it, or try adding your city or ZIP code.`
      : `We could not find districts at your location. The site covers ${COVERED_STATES}.`,
    field: typed,
  }
}

/** Test seam: forget every lookup. */
export function resetLookups(): void {
  entries.clear()
  notify()
}

function request(query: string): Promise<SettledLookup> {
  const known = entries.get(query)

  if (known !== undefined && known.state.status !== 'error') {
    return known.answer
  }

  const answer = fetchLookup(query).then((state) => {
    const entry = entries.get(query)

    // Gone only if the lookups were reset while this one was in flight.
    if (entry !== undefined) {
      entry.state = state
      notify()
    }

    return state
  })

  entries.set(query, { state: LOADING, answer })
  notify()

  return answer
}

async function fetchLookup(query: string): Promise<SettledLookup> {
  try {
    const response = await fetch(`/api/districts?${query}`, {
      headers: { Accept: 'application/json' },
      signal: AbortSignal.timeout(TIMEOUT_MS),
    })

    // 404: no districts there. 400: a query the server won't take. Asking
    // again would get the same answer, so neither is an error.
    if (response.status === 404 || response.status === 400) {
      return { status: 'no-match' }
    }

    if (!response.ok) {
      return { status: 'error' }
    }

    const lookup = (await response.json()) as DistrictLookup

    return lookup.districts.length === 0
      ? { status: 'no-match' }
      : { status: 'found', lookup }
  } catch {
    // A dropped connection, the timeout, or a body that isn't JSON.
    return { status: 'error' }
  }
}

function subscribe(onChange: () => void): () => void {
  listeners.add(onChange)
  return () => listeners.delete(onChange)
}

function notify(): void {
  for (const listener of listeners) {
    listener()
  }
}
