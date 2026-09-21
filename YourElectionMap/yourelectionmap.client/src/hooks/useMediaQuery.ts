import { useCallback, useSyncExternalStore } from 'react'

/**
 * The layout breakpoint. Every stylesheet switches at the same 860px; keep
 * the two in step.
 */
export const PHONE_QUERY = '(max-width: 860px)'

/**
 * Whether a media query matches, kept current as the window changes. False
 * wherever matchMedia is missing, which includes jsdom, so tests render the
 * desktop layout unless they stub it.
 */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (typeof window.matchMedia !== 'function') {
        return () => {}
      }

      const list = window.matchMedia(query)
      list.addEventListener('change', onChange)

      return () => list.removeEventListener('change', onChange)
    },
    [query],
  )

  const getSnapshot = () =>
    typeof window.matchMedia === 'function' && window.matchMedia(query).matches

  return useSyncExternalStore(subscribe, getSnapshot, () => false)
}
