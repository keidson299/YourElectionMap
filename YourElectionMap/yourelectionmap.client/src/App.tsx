import { HomePage } from './pages/HomePage'
import { MapPage } from './pages/MapPage'
import { useRoute } from './routing/useRoute'

function App() {
  const route = useRoute()

  if (route.name === 'map') {
    return <MapPage address={route.address} coords={route.coords} />
  }

  return <HomePage />
}

export default App
