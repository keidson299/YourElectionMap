import { describe, expect, it } from 'vitest'
import { layerRole, mapLayers, resolveSelection } from './selection'
import type { District, DistrictLevel } from './types'

function district(id: string, level: DistrictLevel): District {
  return {
    id,
    level,
    office: id,
    number: '1',
    detail: '',
    area: null,
    boundary: [],
  }
}

const federal = district('federal', 'congressional')
const senate = district('senate', 'state-upper')
const house = district('house', 'state-lower')
const county = district('county', 'county')
const city = district('city', 'city')
const school = district('school', 'school')

const ALL = [federal, senate, house, county, city, school]

function ids(layers: ReturnType<typeof mapLayers>) {
  return [layers.selected.id, layers.dashed?.id ?? null, layers.dotted?.id ?? null]
}

describe('resolveSelection', () => {
  it('honours a requested district the lookup has', () => {
    expect(resolveSelection(ALL, 'county')).toBe(county)
  })

  it('falls back to the lower state chamber', () => {
    expect(resolveSelection(ALL, null)).toBe(house)
    expect(resolveSelection(ALL, 'no-such-district')).toBe(house)
  })

  it('falls back to the one chamber where there is no lower one', () => {
    expect(resolveSelection([federal, senate, county], null)).toBe(senate)
  })

  it('falls back to the first district where there is no state chamber', () => {
    expect(resolveSelection([federal, county], null)).toBe(federal)
  })
})

describe('mapLayers', () => {
  it('draws one neighbour on each side, the broader one dashed', () => {
    expect(ids(mapLayers(ALL, house))).toEqual(['house', 'senate', 'county'])
  })

  it('takes both neighbours from one side at the start of the list', () => {
    expect(ids(mapLayers(ALL, federal))).toEqual(['federal', 'senate', 'house'])
  })

  it('takes both neighbours from one side at the end of the list', () => {
    expect(ids(mapLayers(ALL, school))).toEqual(['school', 'county', 'city'])
  })

  it('draws one neighbour when there are only two districts', () => {
    expect(ids(mapLayers([federal, county], county))).toEqual([
      'county',
      'federal',
      null,
    ])
  })

  it('draws the selection alone when it is the only district', () => {
    expect(ids(mapLayers([county], county))).toEqual(['county', null, null])
  })

  it('tells each row which way it is drawn', () => {
    const layers = mapLayers(ALL, house)

    expect(ALL.map((each) => layerRole(layers, each))).toEqual([
      'none',
      'dashed',
      'selected',
      'dotted',
      'none',
      'none',
    ])
  })
})
