import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DistrictRail } from './DistrictRail'
import { sampleLookup } from '../../test/sampleLookup'
import { mapLayers, resolveSelection } from '../../districts/selection'
import type { ElectionSummary } from '../../districts/types'

const lookup = sampleLookup({ address: '1420 Maple Street, Springfield' })

function renderRail(
  options: {
    selectedId?: string
    election?: ElectionSummary | null
    source?: string | null
  } = {},
) {
  const onSelect = vi.fn()
  const selected = resolveSelection(lookup.districts, options.selectedId ?? null)

  if (selected === undefined) {
    throw new Error('The sample always has districts.')
  }

  render(
    <DistrictRail
      districts={lookup.districts}
      layers={mapLayers(lookup.districts, selected)}
      election={options.election === undefined ? lookup.election : options.election}
      source={options.source === undefined ? lookup.source : options.source}
      compact={false}
      sheetExpanded={false}
      onToggleSheet={() => {}}
      onSelect={onSelect}
    />,
  )

  return { onSelect }
}

describe('DistrictRail', () => {
  it('lists every district as one radio group named by its heading', () => {
    renderRail()

    const group = screen.getByRole('radiogroup', {
      name: '6 districts cover you',
    })

    expect(within(group).getAllByRole('radio')).toHaveLength(6)
  })

  it('checks exactly the selected district and says it is on the map', () => {
    renderRail()

    const checked = screen
      .getAllByRole('radio')
      .filter((radio) => (radio as HTMLInputElement).checked)

    expect(checked).toHaveLength(1)
    expect(checked[0]).toHaveAccessibleName('State House · District 44')
    expect(checked[0]).toHaveAccessibleDescription('Shown on the map')
  })

  it('describes the other rows by where they are', () => {
    renderRail()

    expect(
      screen.getByRole('radio', { name: 'County Commission · District 3' }),
    ).toHaveAccessibleDescription('Greenlake County')
  })

  it('hands a clicked district to the caller', async () => {
    const { onSelect } = renderRail()

    await userEvent.click(
      screen.getByRole('radio', { name: 'City Council · Ward 2' }),
    )

    expect(onSelect).toHaveBeenCalledWith('city-2')
  })

  it('moves the selection with the arrow keys', async () => {
    const { onSelect } = renderRail()

    screen.getByRole('radio', { name: 'State House · District 44' }).focus()
    await userEvent.keyboard('{ArrowDown}')

    expect(onSelect).toHaveBeenCalledWith('county-3')
  })

  it('shows the election details for the address', () => {
    renderRail()

    expect(screen.getByText('Tue, Nov 3')).toBeInTheDocument()
    expect(screen.getByText('14 candidates')).toBeInTheDocument()
    expect(screen.getByText('2 ballot measures')).toBeInTheDocument()
  })

  it('leaves the election details out when there are none', () => {
    renderRail({ election: null })

    expect(screen.queryByText('Also for this address')).toBeNull()
  })

  it('credits the data the boundaries came from', () => {
    renderRail({ source: 'NC 2024 precincts' })

    expect(screen.getByText('NC 2024 precincts')).toBeInTheDocument()
  })

  it('names the Redistricting Data Hub under the districts, with a link', () => {
    renderRail()

    expect(
      screen.getByRole('link', { name: 'Redistricting Data Hub' }),
    ).toHaveAttribute('href', 'https://redistrictingdatahub.org/')
  })

  it('leaves the source tag out when there is no source to name', () => {
    renderRail({ source: null })

    expect(document.querySelector('.source-tag')).toBeNull()
  })
})
