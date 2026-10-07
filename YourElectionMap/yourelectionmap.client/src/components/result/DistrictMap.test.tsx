import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { DistrictMap } from './DistrictMap'
import { sampleLookup } from '../../test/sampleLookup'
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

  // The boundaries here are real places. A hand-drawn river, road or neighbouring
  // district would be saying something about those places that is not true.
  it('draws nothing but paper under the real boundaries', () => {
    const { svg } = renderMap()

    expect(svg.querySelector('.survey-ground__paper')).not.toBeNull()
    expect(svg.querySelector('.survey-ground__water')).toBeNull()
    expect(svg.querySelector('.survey-ground__roads')).toBeNull()
    expect(svg.querySelector('.survey-ground__mesh')).toBeNull()
  })

  it('lays the paper under wherever the address is', () => {
    const { svg } = renderMap()
    const paper = svg.querySelector('.survey-ground__paper')

    expect(Number(paper?.getAttribute('x'))).toBeLessThan(817)
    expect(Number(paper?.getAttribute('width'))).toBeGreaterThan(620)
  })

  // Hatching a district with a hole in it. A dissolved boundary does not wind
  // consistently, so the default fill rule would fill holes and skip islands.
  it('hatches the selection by the even-odd rule', () => {
    const { svg } = renderMap()

    expect(svg.querySelector('.district-map__hatch')).toHaveAttribute(
      'fill-rule',
      'evenodd',
    )
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
    expect(zoomIn).toHaveAttribute('aria-disabled', 'true')

    await userEvent.click(zoomOut)
    await userEvent.click(zoomOut)
    await userEvent.click(zoomOut)
    expect(viewBoxWidth()).toBeGreaterThan(620)
    expect(zoomOut).toHaveAttribute('aria-disabled', 'true')
    expect(zoomIn).not.toHaveAttribute('aria-disabled')
  })

  // aria-disabled rather than disabled, so a keyboard user is not dropped
  // back to the top of the page when a limit is reached.
  it('keeps focus on a zoom button that has reached its limit', async () => {
    const { viewBoxWidth } = renderMap()
    const zoomIn = screen.getByRole('button', { name: 'Zoom in' })

    zoomIn.focus()
    await userEvent.keyboard('{Enter}{Enter}')
    const closest = viewBoxWidth()
    await userEvent.keyboard('{Enter}')

    expect(zoomIn).toHaveFocus()
    expect(viewBoxWidth()).toBe(closest)
  })
})
