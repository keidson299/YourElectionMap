// A minimal history store over the browser's own navigation API. The app has
// two routes and no need for a router dependency; anything that changes the
// URL goes through navigate() so useSyncExternalStore hears about it.

const NAVIGATION_EVENT = 'yem:navigation'

export function getSnapshot(): string {
  return `${window.location.pathname}${window.location.search}`
}

export function subscribe(onChange: () => void): () => void {
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
