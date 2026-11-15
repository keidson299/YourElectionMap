import { describe, expect, it } from 'vitest'
import { countOf, districtName, formatElectionDate } from './labels'
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
    boundary: [],
    ...overrides,
  }
}

describe('districtName', () => {
  it('names a numbered district in each form', () => {
    const stateHouse = district({})

    expect(districtName(stateHouse)).toBe('State House · District 44')
    expect(districtName(stateHouse, 'short')).toBe('State House · 44')
    expect(districtName(stateHouse, 'legend')).toBe('State House 44')
  })

  it('names a county, city or school district by its name alone in every form', () => {
    const county = district({
      id: 'county-37183',
      level: 'county',
      office: 'Wake County',
      unit: null,
      number: null,
      detail: 'County',
    })

    expect(districtName(county)).toBe('Wake County')
    expect(districtName(county, 'short')).toBe('Wake County')
    expect(districtName(county, 'legend')).toBe('Wake County')
  })
})

describe('countOf', () => {
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

  // The server sends a DateOnly, but a DateTime would serialise with a time part.
  it('ignores a time part', () => {
    expect(formatElectionDate('2026-11-03T00:00:00')).toBe('Tue, Nov 3')
  })

  it('hands back a value it cannot read instead of throwing', () => {
    expect(formatElectionDate('soon')).toBe('soon')
  })
})
