// The URL is the app's source of truth for what is being looked up. Slice 3
// reads these values to call the districts API; for now they only decide which
// screen renders and what the map shell echoes back.

export type Coordinates = {
  lat: number
  lon: number
}

export type Route =
  | { name: 'home' }
  | { name: 'map'; address: string | null; coords: Coordinates | null }

export const HOME_URL = '/'

const HOME: Route = { name: 'home' }

export function parseRoute(location: string): Route {
  const [path = '', query = ''] = location.split('?')

  if (normalizePath(path) !== '/map') {
    return HOME
  }

  const params = new URLSearchParams(query)
  const address = normalizeAddress(params.get('address'))
  const coords = parseCoordinates(params.get('lat'), params.get('lon'))

  // A map URL with nothing to look up has nothing to show.
  if (address === null && coords === null) {
    return HOME
  }

  return { name: 'map', address, coords }
}

export function mapUrl(target: { address: string } | { coords: Coordinates }): string {
  const params = new URLSearchParams()

  if ('address' in target) {
    params.set('address', target.address)
  } else {
    params.set('lat', formatCoordinate(target.coords.lat))
    params.set('lon', formatCoordinate(target.coords.lon))
  }

  return `/map?${params.toString()}`
}

function normalizePath(path: string): string {
  const trimmed = path.replace(/\/+$/, '')
  return trimmed === '' ? '/' : trimmed.toLowerCase()
}

function normalizeAddress(value: string | null): string | null {
  const trimmed = value?.trim() ?? ''
  return trimmed === '' ? null : trimmed
}

function parseCoordinates(
  rawLat: string | null,
  rawLon: string | null,
): Coordinates | null {
  if (rawLat === null || rawLon === null) {
    return null
  }

  const lat = Number(rawLat)
  const lon = Number(rawLon)

  if (!Number.isFinite(lat) || !Number.isFinite(lon)) {
    return null
  }

  if (Math.abs(lat) > 90 || Math.abs(lon) > 180) {
    return null
  }

  return { lat, lon }
}

// Six decimal places is roughly a tenth of a metre — more than a district
// lookup needs, and short enough to keep the URL readable.
function formatCoordinate(value: number): string {
  return String(Math.round(value * 1e6) / 1e6)
}
