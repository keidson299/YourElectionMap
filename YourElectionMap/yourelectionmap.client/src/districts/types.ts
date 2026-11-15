// The twin of the server's Districts/DistrictLookup.cs; change the two together.

/** Broad to local, the order the server sends districts in. */
export type DistrictLevel =
  | 'congressional'
  | 'state-upper'
  | 'state-lower'
  | 'county'
  | 'city'
  | 'school'

/** [longitude, latitude], the order GeoJSON and MapLibre use. */
export type LngLat = readonly [longitude: number, latitude: number]

/** An outer ring, then any holes in it; each ring is closed. */
export type Polygon = readonly (readonly LngLat[])[]

export type District = {
  id: string
  level: DistrictLevel
  office: string
  /** Null, with the number, for a district known by name: 'Wake County'. */
  unit: 'District' | null
  number: string | null
  detail: string
  area: string | null
  /** A GeoJSON MultiPolygon's coordinates. */
  boundary: readonly Polygon[]
}

export type ElectionSummary = {
  /** YYYY-MM-DD */
  date: string
  /** Null when the election data holds no contests: unknown, not zero. */
  candidates: number | null
  measures: number | null
}

export type DistrictLookup = {
  label: string
  location: LngLat
  /** Never empty, and never assume six. */
  districts: readonly District[]
  election: ElectionSummary | null
  source: string | null
}
