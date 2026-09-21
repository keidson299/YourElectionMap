import type { District } from './types'

// Every string the result page builds from district data, in one place so the
// rail, the map chip and the legend never disagree about a name.

/**
 * - full: 'State House · District 44', for the rail and the desktop chip
 * - short: 'State House · 44', for the phone chip
 * - legend: 'State House 44', for the map legend
 *
 * Wards and zones keep their unit in every form ('City Council · Ward 2'),
 * because a bare number would not say which kind it is.
 */
export type NameForm = 'full' | 'short' | 'legend'

export function districtName(district: District, form: NameForm = 'full'): string {
  const { office, unit, number } = district
  const numberOnly = unit === 'District'

  switch (form) {
    case 'short':
      return numberOnly ? `${office} · ${number}` : `${office} · ${unit} ${number}`
    case 'legend':
      return numberOnly ? `${office} ${number}` : `${office} ${unit} ${number}`
    default:
      return `${office} · ${unit} ${number}`
  }
}

export function coverageTitle(count: number): string {
  return count === 1 ? '1 district covers you' : `${count} districts cover you`
}

export function countOf(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`
}

const ELECTION_DATE = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  month: 'short',
  day: 'numeric',
  // The date has no time of day, so read and print it in UTC; local time
  // would move it back a day anywhere west of Greenwich.
  timeZone: 'UTC',
})

/**
 * '2026-11-03' becomes 'Tue, Nov 3'. Any time part is ignored, and a value
 * that is not a date comes back as given rather than throwing mid-render.
 */
export function formatElectionDate(isoDate: string): string {
  const date = new Date(`${isoDate.slice(0, 10)}T00:00:00Z`)
  return Number.isNaN(date.getTime()) ? isoDate : ELECTION_DATE.format(date)
}

const WHOLE_NUMBER = new Intl.NumberFormat('en-US')

/** 'Greenlake County · 71,400 residents', or whichever half is known. */
export function districtMeta(district: District): string | null {
  const parts = [
    district.area,
    district.population === null
      ? null
      : `${WHOLE_NUMBER.format(district.population)} ${
          district.population === 1 ? 'resident' : 'residents'
        }`,
  ].filter((part) => part !== null)

  return parts.length === 0 ? null : parts.join(' · ')
}
