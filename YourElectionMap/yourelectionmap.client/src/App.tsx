import { useEffect, useRef } from 'react'
import { HomePage } from './pages/HomePage'
import { MapPage } from './pages/MapPage'
import { useRoute } from './routing/history'
import { mapUrl } from './routing/routes'

function App() {
  const route = useRoute()
  // Changes with the page or the lookup, but not with a district picked on it.
  const pageKey = route.name === 'map' ? mapUrl(route.target) : '/'
  const title =
    route.name === 'map'
      ? `Districts for ${'address' in route.target ? route.target.address : 'your location'} — YourElectionMap`
      : 'YourElectionMap — see the district maps you vote in'
  const landingPage = useRef<string | null>(pageKey)

  useEffect(() => {
    document.title = title

    // Leave focus alone on the page the visit started on; after that, move it
    // to the new page's heading so a screen reader announces where it landed.
    if (pageKey === landingPage.current) {
      return
    }

    landingPage.current = null
    document.querySelector<HTMLElement>('h1')?.focus()
  }, [pageKey, title])

  if (route.name === 'map') {
    return <MapPage target={route.target} district={route.district} />
  }

  return <HomePage />
}

export default App
