// The URL decides what is looked up and which district is on the map.

export type Coordinates = { lat: number; lon: number }

export type LookupTarget = { address: string } | { coords: Coordinates }

export type Route =
  | { name: 'home' }
  | { name: 'map'; target: LookupTarget; district: string | null }

export function parseRoute(location: string): Route {
  const [path = '', query = ''] = location.split('?')

  if (path.replace(/\/+$/, '').toLowerCase() !== '/map') {
    return { name: 'home' }
  }

  const params = new URLSearchParams(query)
  const address = normalizeText(params.get('address'))
  const district = normalizeText(params.get('district'))

  if (address !== null) {
    return { name: 'map', target: { address }, district }
  }

  // Blank values must not reach Number(), which reads '' as 0.
  const rawLat = normalizeText(params.get('lat'))
  const rawLon = normalizeText(params.get('lon'))
  const lat = Number(rawLat ?? NaN)
  const lon = Number(rawLon ?? NaN)

  if (Math.abs(lat) <= 90 && Math.abs(lon) <= 180) {
    return { name: 'map', target: { coords: { lat, lon } }, district }
  }

  return { name: 'home' }
}

/** The target as query parameters: the page URL, the API and the lookup cache all use it. */
export function targetQuery(target: LookupTarget): string {
  const params = new URLSearchParams()

  if ('address' in target) {
    params.set('address', target.address)
  } else {
    // Five places is about a metre: finer than the data, and no more exact
    // about where a visitor stood than a link needs.
    params.set('lat', String(Math.round(target.coords.lat * 1e5) / 1e5))
    params.set('lon', String(Math.round(target.coords.lon * 1e5) / 1e5))
  }

  return params.toString()
}

export function mapUrl(target: LookupTarget, district?: string): string {
  const query = targetQuery(target)

  return district
    ? `/map?${query}&district=${encodeURIComponent(district)}`
    : `/map?${query}`
}

function normalizeText(value: string | null): string | null {
  const trimmed = value?.trim() ?? ''
  return trimmed === '' ? null : trimmed
}
