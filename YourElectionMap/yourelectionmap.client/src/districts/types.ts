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
 * A point in map units, the coordinate space everything drawn on the result map
 * shares. One unit is 96 feet, so 55 of them make a mile: see
 * `MAP_UNITS_PER_MILE` and the server's `StatePlane.FeetPerMapUnit`. Y counts
 * downward, the way SVG does, so north is up.
 */
export type SurveyPoint = readonly [x: number, y: number]

/** One closed ring of a boundary. */
export type SurveyRing = readonly SurveyPoint[]

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
  /** Rings rather than one polygon, so real islands and holes fit unchanged. */
  boundary: readonly SurveyRing[]
}

export type ElectionSummary = {
  /** YYYY-MM-DD */
  date: string
  candidates: number
  measures: number
}

export type DistrictLookup = {
  /** The address as the page names it: '1420 Maple Street'. */
  label: string
  /** The address in map units. */
  location: SurveyPoint
  /** One or more districts. Never assume six. */
  districts: readonly District[]
  /** Null hides the rail footer, for addresses with no election info. */
  election: ElectionSummary | null
  /** What the boundaries were drawn from, for the rail to credit. */
  source: string | null
}
