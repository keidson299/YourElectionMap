import type { District, DistrictLookup, LngLat, Polygon } from '../districts/types'
import type { LookupTarget } from '../routing/routes'

// An invented fixture, so component tests can render a result without a fetch.
// Unlike the server's three levels, it has all six on purpose: it is the only
// thing exercising a ward, a zone, an area, a population and the election
// footer. DistrictsEndpointTests pins what the server really sends.
//
// The shapes are written in the stand-in drawing's units, as the spec draws them,
// and placed on the globe by `at`: the spec's address lands on downtown Raleigh,
// one drawing unit is SHEET_DEGREES (about a hundred feet), and the drawing's y,
// which counts down, becomes latitude, which counts up. State House 44, the
// county and the school zone are the spec's shapes; the other three are made up,
// each containing the address.

const SHEET_ADDRESS = [1127, 629] as const
const ADDRESS_LNG_LAT: LngLat = [-78.64, 35.78]
const SHEET_DEGREES = 0.0003

/** A point of the drawing, on the globe. */
function at(x: number, y: number): LngLat {
  return [
    round(ADDRESS_LNG_LAT[0] + (x - SHEET_ADDRESS[0]) * SHEET_DEGREES),
    round(ADDRESS_LNG_LAT[1] - (y - SHEET_ADDRESS[1]) * SHEET_DEGREES),
  ]
}

/** One polygon from the drawing's corners, closed as the contract's rings are. */
function sheet(...corners: [number, number][]): Polygon[] {
  const ring = corners.map(([x, y]) => at(x, y))

  return [[[...ring, ring[0]]]]
}

function round(value: number): number {
  return Math.round(value * 1e5) / 1e5
}

const SAMPLE_DISTRICTS: readonly District[] = [
  {
    id: 'congressional-7',
    level: 'congressional',
    office: 'U.S. House',
    unit: 'District',
    number: '7',
    detail: 'Federal',
    area: null,
    population: 761200,
    boundary: sheet([640, 250], [1560, 270], [1540, 985], [680, 960]),
  },
  {
    id: 'state-upper-22',
    level: 'state-upper',
    office: 'State Senate',
    unit: 'District',
    number: '22',
    detail: 'State',
    area: null,
    population: 142800,
    boundary: sheet([800, 360], [1420, 390], [1400, 880], [830, 850]),
  },
  {
    id: 'state-lower-44',
    level: 'state-lower',
    office: 'State House',
    unit: 'District',
    number: '44',
    detail: 'State',
    area: 'Greenlake County',
    population: 71400,
    boundary: sheet([930, 495], [1310, 520], [1270, 735], [1000, 765]),
  },
  {
    id: 'county-3',
    level: 'county',
    office: 'County Commission',
    unit: 'District',
    number: '3',
    detail: 'Greenlake County',
    area: 'Greenlake County',
    population: 58900,
    boundary: sheet([700, 300], [1520, 330], [1480, 930], [740, 900]),
  },
  {
    id: 'city-2',
    level: 'city',
    office: 'City Council',
    unit: 'Ward',
    number: '2',
    detail: 'City of Springfield',
    area: 'City of Springfield',
    population: 24600,
    boundary: sheet([1030, 540], [1250, 555], [1235, 720], [1050, 735]),
  },
  {
    id: 'school-5',
    level: 'school',
    office: 'School Board',
    unit: 'Zone',
    number: '5',
    detail: 'Springfield Unified',
    area: 'Springfield Unified',
    population: 31200,
    boundary: sheet([965, 455], [1345, 478], [1305, 800], [1010, 825]),
  },
]

export function sampleLookup(target: LookupTarget): DistrictLookup {
  return {
    label: 'address' in target ? streetLine(target.address) : 'Your location',
    location: ADDRESS_LNG_LAT,
    districts: SAMPLE_DISTRICTS,
    election: { date: '2026-11-03', candidates: 14, measures: 2 },
    source: 'Sample data',
  }
}

/** '1420 Maple Street, Springfield' is named '1420 Maple Street' on the map. */
function streetLine(address: string): string {
  return address.split(',')[0].trim() || address
}
