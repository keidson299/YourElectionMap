// The shape the result page renders, fetched from the districts API. The server's
// twin of these types is Districts/DistrictLookup.cs; change the two together.

/** Broad to local, the order the server sends districts in, which the rail and the map's neighbour rule read. */
export const DISTRICT_LEVELS = [
  'congressional',
  'state-upper',
  'state-lower',
  'county',
  'city',
  'school',
] as const

export type DistrictLevel = (typeof DISTRICT_LEVELS)[number]

/** A point as [longitude, latitude], the order GeoJSON and the street map use. */
export type LngLat = readonly [longitude: number, latitude: number]

/** One closed ring: the last point repeats the first. */
export type Ring = readonly LngLat[]

/** An outer ring, then any holes in it. */
export type Polygon = readonly Ring[]

export type District = {
  /** URL-safe and stable, e.g. 'state-lower-44', or 'county-37183' for a county. */
  id: string
  level: DistrictLevel
  /** 'State House', or a whole government's name: 'Wake County'. */
  office: string
  /** Null for a county, city or school district, which is named, not numbered. */
  unit: 'District' | 'Ward' | 'Zone' | null
  /** '44', or null on the same terms as the unit. */
  number: string | null
  /** The rail's second line: 'Federal', 'State'. */
  detail: string
  /** The place named on the map chip, where it is known. */
  area: string | null
  /**
   * A GeoJSON MultiPolygon's coordinates, so a district with islands or holes is
   * drawn as one. The server has already sorted which ring is which.
   */
  boundary: readonly Polygon[]
}

export type ElectionSummary = {
  /** YYYY-MM-DD */
  date: string
  /**
   * Candidates across the ballot, or null when the election data holds no
   * contests: a ballot nobody has seen is not an empty one.
   */
  candidates: number | null
  /** Referenda on the ballot, or null on the same terms. */
  measures: number | null
}

export type DistrictLookup = {
  /** The address as the page names it: '1420 Maple Street'. */
  label: string
  /** Where the address is. */
  location: LngLat
  /** One or more districts. Never assume six. */
  districts: readonly District[]
  /** Null hides the rail footer: no election, or none the server could find. */
  election: ElectionSummary | null
  /** What the boundaries were drawn from, for the rail to credit. */
  source: string | null
}
