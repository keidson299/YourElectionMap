import { useSyncExternalStore } from 'react'

/** The layout breakpoint. Every stylesheet switches at the same 860px. */
const PHONE_QUERY = '(max-width: 860px)'

function subscribe(onChange: () => void): () => void {
  // jsdom has no matchMedia, so tests get the desktop layout unless they stub it.
  if (typeof window.matchMedia !== 'function') {
    return () => {}
  }

  const list = window.matchMedia(PHONE_QUERY)
  list.addEventListener('change', onChange)
  return () => list.removeEventListener('change', onChange)
}

function getSnapshot(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia(PHONE_QUERY).matches
}

export function useIsPhone(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, () => false)
}
