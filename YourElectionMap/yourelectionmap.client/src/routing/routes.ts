// The URL is the app's source of truth for what is being looked up and which
// district is on the map.

export type Coordinates = {
  lat: number
  lon: number
}

/** What a lookup starts from: a typed address, or the browser's location. */
export type LookupTarget = { address: string } | { coords: Coordinates }

export type Route =
  | { name: 'home' }
  | { name: 'map'; target: LookupTarget; district: string | null }

export const HOME_URL = '/'

const HOME: Route = { name: 'home' }

export function parseRoute(location: string): Route {
  const [path = '', query = ''] = location.split('?')

  if (normalizePath(path) !== '/map') {
    return HOME
  }

  const params = new URLSearchParams(query)
  const address = normalizeText(params.get('address'))
  // Blank values must not reach Number(), which reads '' as 0.
  const coords = parseCoordinates(
    normalizeText(params.get('lat')),
    normalizeText(params.get('lon')),
  )
  const district = normalizeText(params.get('district'))

  if (address !== null) {
    return { name: 'map', target: { address }, district }
  }

  if (coords !== null) {
    return { name: 'map', target: { coords }, district }
  }

  // A map URL with nothing to look up has nothing to show.
  return HOME
}

/**
 * What is being looked up, as query parameters. The page URL and the districts
 * API take the same ones, and it doubles as the key a lookup is cached under.
 */
export function targetQuery(target: LookupTarget): string {
  const params = new URLSearchParams()

  if ('address' in target) {
    params.set('address', target.address)
  } else {
    params.set('lat', formatCoordinate(target.coords.lat))
    params.set('lon', formatCoordinate(target.coords.lon))
  }

  return params.toString()
}

export function mapUrl(
  target: LookupTarget,
  options: { district?: string | null } = {},
): string {
  const query = targetQuery(target)

  if (!options.district) {
    return `/map?${query}`
  }

  return `/map?${query}&district=${encodeURIComponent(options.district)}`
}

function normalizePath(path: string): string {
  const trimmed = path.replace(/\/+$/, '')
  return trimmed === '' ? '/' : trimmed.toLowerCase()
}

function normalizeText(value: string | null): string | null {
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

// Five decimal places is about a metre: as fine as the district files, and
// no more exact about where a visitor stood than a shared link needs to be.
function formatCoordinate(value: number): string {
  return String(Math.round(value * 1e5) / 1e5)
}
