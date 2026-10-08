// The shape the result page renders, fetched from the districts API. The server's
// twin of these types is Districts/DistrictLookup.cs; change the two together.

/** Broad to local. The rail lists districts in this order and the map's neighbour rule reads it. */
export const DISTRICT_LEVELS = [
  'congressional',
  'state-upper',
  'state-lower',
  'county',
  'city',
  'school',
] as const

export type DistrictLevel = (typeof DISTRICT_LEVELS)[number]

/**
 * A point as [longitude, latitude], the order GeoJSON and the street map use.
 * The server answers "which district" in its own projected units, but everything
 * it sends the page is in degrees.
 */
export type LngLat = readonly [longitude: number, latitude: number]

/** One closed ring: the last point repeats the first. */
export type Ring = readonly LngLat[]

/** An outer ring, then any holes in it. */
export type Polygon = readonly Ring[]

export type District = {
  /** URL-safe and stable, e.g. 'state-lower-44'. */
  id: string
  level: DistrictLevel
  /** 'State House' */
  office: string
  unit: 'District' | 'Ward' | 'Zone'
  /** '44' */
  number: string
  /** The rail's second line: 'Federal', 'State'. */
  detail: string
  /** The place named on the map chip, where it is known. */
  area: string | null
  population: number | null
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
