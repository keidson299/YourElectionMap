import { useEffect, useRef } from 'react'
import { HomePage } from './pages/HomePage'
import { MapPage } from './pages/MapPage'
import { HOME_URL, mapUrl, type Route } from './routing/routes'
import { useRoute } from './routing/useRoute'

const HOME_TITLE = 'YourElectionMap — see the district maps you vote in'

function App() {
  const route = useRoute()
  // A new page or a new lookup, but not a district picked on the same one.
  const pageKey = route.name === 'map' ? mapUrl(route.target) : HOME_URL
  const title = pageTitle(route)
  const landingPage = useRef<string | null>(pageKey)

  useEffect(() => {
    document.title = title

    // Leave focus alone on the page the visit started on. After that, move it
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

function pageTitle(route: Route): string {
  if (route.name !== 'map') {
    return HOME_TITLE
  }

  const place =
    'address' in route.target ? route.target.address : 'your location'

  return `Districts for ${place} — YourElectionMap`
}

export default App
