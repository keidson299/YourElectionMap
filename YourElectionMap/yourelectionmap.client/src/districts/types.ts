// The shape the result page renders. Slice 2 fills it from an invented sample;
// slice 3 fetches it from the districts API, and slice 4 makes it real.

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
 * A point in the map's survey units: the stand-in drawing's coordinate space
 * until slice 5 projects real geometry into it.
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
  /** The rail's second line: 'Federal', 'Greenlake County'. */
  detail: string
  /** The place named on the map chip: 'Greenlake County'. */
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
  /** The address in survey units. */
  location: SurveyPoint
  /** One or more districts. Never assume six. */
  districts: readonly District[]
  /** Null hides the rail footer, for addresses with no election info. */
  election: ElectionSummary | null
  /** True while the districts are invented rather than looked up. */
  sample: boolean
}
