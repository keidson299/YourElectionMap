import type { District } from './types'

/**
 * 'State House · District 44' (full: rail, desktop chip), 'State House · 44'
 * (short: phone chip) or 'State House 44' (legend). A named district is its
 * name in every form.
 */
export function districtName(
  district: District,
  form: 'full' | 'short' | 'legend' = 'full',
): string {
  const { office, number } = district

  if (number === null) {
    return office
  }

  switch (form) {
    case 'short':
      return `${office} · ${number}`
    case 'legend':
      return `${office} ${number}`
    default:
      return `${office} · District ${number}`
  }
}

export function countOf(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`
}

const ELECTION_DATE = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  month: 'short',
  day: 'numeric',
  // A bare date read in local time lands a day early anywhere west of Greenwich.
  timeZone: 'UTC',
})

/** '2026-11-03' becomes 'Tue, Nov 3'; anything unreadable comes back as given. */
export function formatElectionDate(isoDate: string): string {
  const date = new Date(`${isoDate.slice(0, 10)}T00:00:00Z`)
  return Number.isNaN(date.getTime()) ? isoDate : ELECTION_DATE.format(date)
}
