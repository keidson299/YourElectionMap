import { useSyncExternalStore } from 'react'
import { getSnapshot, subscribe } from './history'
import { parseRoute, type Route } from './routes'

export function useRoute(): Route {
  const location = useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
  return parseRoute(location)
}
