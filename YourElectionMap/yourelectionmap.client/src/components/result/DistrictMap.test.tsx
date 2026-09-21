import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { DistrictMap } from './DistrictMap'
import { sampleLookup } from '../../districts/sampleLookup'
import { mapLayers, resolveSelection } from '../../districts/selection'

const lookup = sampleLookup({ address: '1420 Maple Street, Springfield' })

function renderMap(selectedId: string | null = null) {
  const selected = resolveSelection(lookup.districts, selectedId)

  if (selected === undefined) {
    throw new Error('The sample always has districts.')
  }

  const { container } = render(
    <DistrictMap
      location={lookup.location}
      label={lookup.label}
      layers={mapLayers(lookup.districts, selected)}
      compact={false}
    />,
  )

  const svg = container.querySelector('svg.district-map__svg')

  if (svg === null) {
    throw new Error('The map did not render its drawing.')
  }

  return {
    svg,
    viewBoxWidth: () => Number(svg.getAttribute('viewBox')?.split(' ')[2]),
  }
}

describe('DistrictMap', () => {
  it('hides the drawing and says in text what it shows', () => {
    const { svg } = renderMap()

    expect(svg).toHaveAttribute('aria-hidden', 'true')
    expect(
      screen.getByRole('heading', { level: 2, name: 'State House · District 44' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Greenlake County · 71,400 residents'),
    ).toBeInTheDocument()
    expect(screen.getByText('1420 Maple Street')).toBeInTheDocument()
  })

  it('keys the selection and both neighbours in the legend', () => {
    renderMap()

    expect(screen.getByText('State House 44 — selected')).toBeInTheDocument()
    expect(screen.getByText('State Senate 22')).toBeInTheDocument()
    expect(screen.getByText('County Commission 3')).toBeInTheDocument()
  })

  it('opens on the spec frame for a district that fits in it', () => {
    const { svg } = renderMap()

    expect(svg).toHaveAttribute('viewBox', '817 381 620 496')
  })

  it('widens the view to take in a large district', () => {
    const { viewBoxWidth } = renderMap('congressional-7')

    expect(viewBoxWidth()).toBeGreaterThan(620)
  })

  it('zooms in twice and out once from where it opens, then stops', async () => {
    const { viewBoxWidth } = renderMap()
    const zoomIn = screen.getByRole('button', { name: 'Zoom in' })
    const zoomOut = screen.getByRole('button', { name: 'Zoom out' })

    await userEvent.click(zoomIn)
    expect(viewBoxWidth()).toBeLessThan(620)
    expect(screen.getByText('0.5 mi')).toBeInTheDocument()

    await userEvent.click(zoomIn)
    expect(zoomIn).toBeDisabled()

    await userEvent.click(zoomOut)
    await userEvent.click(zoomOut)
    await userEvent.click(zoomOut)
    expect(viewBoxWidth()).toBeGreaterThan(620)
    expect(zoomOut).toBeDisabled()
    expect(zoomIn).toBeEnabled()
  })
})
