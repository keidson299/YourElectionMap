import { useSyncExternalStore } from 'react'
import { parseRoute, type Route } from './routes'

// The app's whole router: anything that changes the URL goes through navigate()
// so useRoute hears about it.

const NAVIGATION_EVENT = 'yem:navigation'

export function getSnapshot(): string {
  return `${window.location.pathname}${window.location.search}`
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener('popstate', onChange)
  window.addEventListener(NAVIGATION_EVENT, onChange)

  return () => {
    window.removeEventListener('popstate', onChange)
    window.removeEventListener(NAVIGATION_EVENT, onChange)
  }
}

export function navigate(to: string, options: { replace?: boolean } = {}): void {
  if (to === getSnapshot()) {
    return
  }

  if (options.replace) {
    window.history.replaceState(null, '', to)
  } else {
    window.history.pushState(null, '', to)
  }

  window.dispatchEvent(new Event(NAVIGATION_EVENT))
}

export function useRoute(): Route {
  return parseRoute(useSyncExternalStore(subscribe, getSnapshot, getSnapshot))
}
