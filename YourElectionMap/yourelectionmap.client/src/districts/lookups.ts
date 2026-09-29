import { useCallback, useEffect, useSyncExternalStore } from 'react'
import type { DistrictLookup } from './types'
import { navigate } from '../routing/history'
import { mapUrl, targetQuery, type LookupTarget } from '../routing/routes'

// The districts API, and the state of every lookup this visit has made. The
// server's half of the contract is DistrictsController; it answers 200 with a
// DistrictLookup, 404 when it cannot place the address, and an error status
// otherwise.
//
// Lookups are kept here rather than in component state so that a check made on
// Home is still in hand when the map page renders, and so that a slow answer
// can only ever fill its own entry.

export type LookupState =
  | { status: 'loading' }
  | { status: 'found'; lookup: DistrictLookup }
  | { status: 'no-match' }
  | { status: 'error' }

/** How a lookup ends: waiting is not an answer. */
export type SettledLookup = Exclude<LookupState, { status: 'loading' }>

/** A lookup that did not come back with districts. */
export type LookupProblem = 'no-match' | 'error'

/**
 * What to say under the address field. `field` marks the address itself as the
 * problem, which flags the input; a lookup that failed does not.
 */
export type LookupNotice = { text: string; field: boolean }

const LOADING: LookupState = { status: 'loading' }
const NO_MATCH: SettledLookup = { status: 'no-match' }
const FAILED: SettledLookup = { status: 'error' }

/** How long to wait before treating a silent server as a failure. */
const TIMEOUT_MS = 15000

type Entry = {
  state: LookupState
  /** The answer, for callers that started the lookup themselves. */
  answer: Promise<SettledLookup>
}

// Keyed by the target's query string, which is also what the request sends.
const entries = new Map<string, Entry>()
const listeners = new Set<() => void>()

/**
 * Look a target up, reusing an answer already here or on its way. A lookup that
 * failed is tried again, which is all a Try again button needs to do.
 */
export function requestLookup(target: LookupTarget): Promise<SettledLookup> {
  return request(targetQuery(target))
}

/**
 * Look the target up, and go to its map if it is there. Resolves to the notice
 * to show under the address field, or null when the page has moved on.
 */
export async function openLookup(
  target: LookupTarget,
  options: { replace?: boolean } = {},
): Promise<LookupNotice | null> {
  const state = await requestLookup(target)

  if (state.status === 'found') {
    navigate(mapUrl(target), options)
    return null
  }

  return problemNotice(state.status, target)
}

/** The state of a target's lookup, and a way to ask again after a failure. */
export function useLookup(target: LookupTarget) {
  const query = targetQuery(target)
  const state = useSyncExternalStore(subscribe, () => stateOf(query))
  const retry = useCallback(() => void request(query), [query])

  useEffect(() => {
    void request(query)
  }, [query])

  return { state, retry }
}

/** One sentence for under the address field. Every one says what to do next. */
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

  return {
    text:
      'address' in target
        ? 'We could not match that address. Try adding your city or ZIP code.'
        : 'We could not find districts at your location. Enter an address instead.',
    field: true,
  }
}

/** The same news, as the map page's card states it. */
export function problemCard(
  problem: LookupProblem,
  target: LookupTarget,
): { heading: string; body: string } {
  if (problem === 'error') {
    return {
      heading: 'The district lookup is not responding',
      body: 'Check your connection, then try again.',
    }
  }

  if ('address' in target) {
    return {
      heading: 'We could not match that address',
      body: 'Try adding your city or ZIP code in the search above.',
    }
  }

  return {
    heading: 'We could not find districts here',
    body: 'Enter your address in the search above.',
  }
}

/** Test seam: forget every lookup, so each test starts from nothing. */
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
    settle(query, state)
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

    if (response.status === 404) {
      return NO_MATCH
    }

    if (!response.ok) {
      return FAILED
    }

    const lookup = (await response.json()) as DistrictLookup

    // The server sends a 404 rather than an empty list, but either way there is
    // nothing to draw.
    return lookup.districts.length === 0
      ? NO_MATCH
      : { status: 'found', lookup }
  } catch {
    // A dropped connection, the timeout, or a body that was not the lookup the
    // contract promises.
    return FAILED
  }
}

function settle(query: string, state: LookupState): void {
  const entry = entries.get(query)

  // Gone only if the lookups were reset while this one was in flight.
  if (entry === undefined) {
    return
  }

  entry.state = state
  notify()
}

function stateOf(query: string): LookupState {
  return entries.get(query)?.state ?? LOADING
}

function subscribe(onChange: () => void): () => void {
  listeners.add(onChange)

  return () => {
    listeners.delete(onChange)
  }
}

function notify(): void {
  for (const listener of listeners) {
    listener()
  }
}
