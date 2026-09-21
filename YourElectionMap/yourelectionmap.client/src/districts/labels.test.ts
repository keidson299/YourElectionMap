import { describe, expect, it } from 'vitest'
import {
  countOf,
  coverageTitle,
  districtMeta,
  districtName,
  formatElectionDate,
} from './labels'
import type { District } from './types'

function district(overrides: Partial<District>): District {
  return {
    id: 'state-lower-44',
    level: 'state-lower',
    office: 'State House',
    unit: 'District',
    number: '44',
    detail: 'State',
    area: 'Greenlake County',
    population: 71400,
    boundary: [],
    ...overrides,
  }
}

describe('districtName', () => {
  const stateHouse = district({})
  const ward = district({ office: 'City Council', unit: 'Ward', number: '2' })

  it('spells out the unit in full', () => {
    expect(districtName(stateHouse)).toBe('State House · District 44')
    expect(districtName(ward)).toBe('City Council · Ward 2')
  })

  it('drops the word District in the short form, but keeps Ward and Zone', () => {
    expect(districtName(stateHouse, 'short')).toBe('State House · 44')
    expect(districtName(ward, 'short')).toBe('City Council · Ward 2')
  })

  it('runs the legend form together without a separator', () => {
    expect(districtName(stateHouse, 'legend')).toBe('State House 44')
    expect(
      districtName(
        district({ office: 'School Board', unit: 'Zone', number: '5' }),
        'legend',
      ),
    ).toBe('School Board Zone 5')
  })
})

describe('counts', () => {
  it('agrees the heading with the number of districts', () => {
    expect(coverageTitle(6)).toBe('6 districts cover you')
    expect(coverageTitle(1)).toBe('1 district covers you')
  })

  it('agrees a noun with its count', () => {
    expect(countOf(14, 'candidate', 'candidates')).toBe('14 candidates')
    expect(countOf(1, 'candidate', 'candidates')).toBe('1 candidate')
    expect(countOf(0, 'ballot measure', 'ballot measures')).toBe(
      '0 ballot measures',
    )
  })
})

describe('formatElectionDate', () => {
  // Reading a bare date as local midnight would print Nov 2 anywhere in the
  // Americas; the output must not depend on where the tests run.
  it('prints the calendar date it was given', () => {
    expect(formatElectionDate('2026-11-03')).toBe('Tue, Nov 3')
    expect(formatElectionDate('2027-01-01')).toBe('Fri, Jan 1')
  })

  // ASP.NET serialises a DateTime with a time part by default.
  it('ignores a time part', () => {
    expect(formatElectionDate('2026-11-03T00:00:00')).toBe('Tue, Nov 3')
  })

  it('hands back a value it cannot read instead of throwing', () => {
    expect(formatElectionDate('soon')).toBe('soon')
  })
})

describe('districtMeta', () => {
  it('names the area and the residents', () => {
    expect(districtMeta(district({}))).toBe(
      'Greenlake County · 71,400 residents',
    )
  })

  it('says only what it knows', () => {
    expect(districtMeta(district({ area: null }))).toBe('71,400 residents')
    expect(districtMeta(district({ population: null }))).toBe(
      'Greenlake County',
    )
    expect(districtMeta(district({ area: null, population: null }))).toBeNull()
  })
})
