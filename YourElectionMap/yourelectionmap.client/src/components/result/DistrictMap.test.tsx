import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { DistrictMap } from './DistrictMap'
import { Map, Marker, workerUrl } from '../../test/fakeMaplibre'
import { sampleLookup } from '../../test/sampleLookup'
import { mapLayers, resolveSelection } from '../../districts/selection'
import {
  MAX_ZOOM,
  MIN_ZOOM,
  ZOOM_STEP,
  maxBoundsFor,
  type Bounds,
} from '../../map/camera'
import type { StyleSpecification } from '../../map/style'

// maplibre-gl is replaced by the fake in src/test/fakeMaplibre.ts, which
// records what the component tells it. What is checked here is that: the
// style it is given, where it is told to look, and the chrome drawn over it.

const lookup = sampleLookup({ address: '1420 Maple Street, Springfield' })

function layersFor(selectedId: string | null = null) {
  const selected = resolveSelection(lookup.districts, selectedId)

  if (selected === undefined) {
    throw new Error('The sample always has districts.')
  }

  return mapLayers(lookup.districts, selected)
}

const sampleLimits = maxBoundsFor(lookup.districts)

function renderMap(selectedId: string | null = null) {
  const view = (id: string | null, maxBounds: Bounds = sampleLimits) => (
    <DistrictMap
      location={lookup.location}
      label={lookup.label}
      layers={layersFor(id)}
      maxBounds={maxBounds}
      compact={false}
    />
  )
  const { rerender } = render(view(selectedId))
  const map = Map.instances.at(-1)

  if (map === undefined) {
    throw new Error('The component did not make a map.')
  }

  return {
    map,
    style: () => map.style as StyleSpecification,
    select: (id: string) => rerender(view(id)),
    limit: (maxBounds: Bounds) => rerender(view(null, maxBounds)),
  }
}

/** The district ids a style draws in one of its district sources. */
function idsIn(style: StyleSpecification, source: string): unknown[] {
  const spec = style.sources[source]

  if (spec?.type !== 'geojson' || typeof spec.data !== 'object') {
    throw new Error(`No GeoJSON source called ${source}.`)
  }

  const data = spec.data as { features: { properties: { id: unknown } }[] }

  return data.features.map((feature) => feature.properties.id)
}

describe('DistrictMap', () => {
  // Without it the bundled library looks for its worker beside our chunk, finds
  // nothing, and draws only the background.
  it('tells the map library where its bundled worker is', () => {
    expect(workerUrl).toMatch(/maplibre-gl-worker.*\.mjs/)
  })

  it('says in text what the drawing shows', () => {
    renderMap()

    expect(
      screen.getByRole('heading', { level: 2, name: 'State House · District 44' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Greenlake County'),
    ).toBeInTheDocument()
    expect(screen.getByText('1420 Maple Street')).toBeInTheDocument()
  })

  it('keys the selection and both neighbours in the legend', () => {
    renderMap()

    expect(screen.getByText('State House 44 — selected')).toBeInTheDocument()
    expect(screen.getByText('State Senate 22')).toBeInTheDocument()
    expect(screen.getByText('County Commission 3')).toBeInTheDocument()
  })

  it('draws the selection and its two neighbours', () => {
    const { style } = renderMap()

    expect(idsIn(style(), 'selected')).toEqual(['state-lower-44'])
    expect(idsIn(style(), 'dashed')).toEqual(['state-upper-22'])
    expect(idsIn(style(), 'dotted')).toEqual(['county-3'])

    // Each boundary goes to the map as a MultiPolygon, holes and all.
    const spec = style().sources.selected
    const data = (spec as { data: { features: { geometry: unknown }[] } }).data

    expect(data.features[0].geometry).toEqual({
      type: 'MultiPolygon',
      coordinates: layersFor().selected.boundary,
    })
  })

  // The district is the point of the map: the ground lies under the streets,
  // and every name under the district lines, so none of them hides a boundary.
  it('keeps the ground under the streets and the names under the districts', () => {
    const { style } = renderMap()
    const layers = style().layers
    const at = (id: string) => layers.findIndex((layer) => layer.id === id)
    const names = layers.flatMap((layer, index) =>
      layer.type === 'symbol' ? [index] : [],
    )

    expect(at('parks')).toBeLessThan(at('streets-minor'))
    expect(at('water')).toBeLessThan(at('streets-minor'))
    expect(Math.min(...names)).toBeGreaterThan(at('selected-hatch'))
    expect(Math.max(...names)).toBeLessThan(at('dashed-line'))
  })

  it('opens centred on the address and wide enough for the district', () => {
    const { map } = renderMap('congressional-7')
    const [[west, south], [east, north]] = map.options.bounds as [
      [number, number],
      [number, number],
    ]
    const [lng, lat] = lookup.location

    expect((west + east) / 2).toBeCloseTo(lng, 6)
    expect((south + north) / 2).toBeCloseTo(lat, 6)

    // The sample's congressional district reaches 487 drawing units west.
    expect(lng - west).toBeGreaterThan(487 * 0.0003)
  })

  it('redraws and refits the same map when the selection changes', () => {
    const { map, style, select } = renderMap()

    select('congressional-7')

    expect(Map.instances).toHaveLength(1)
    expect(idsIn(style(), 'selected')).toEqual(['congressional-7'])
    expect(map.fitted).toHaveLength(1)
  })

  it('keeps the map near the lookup, and moves its limits when the lookup moves', () => {
    const { map, limit } = renderMap()

    expect(map.options.maxBounds).toEqual(sampleLimits)
    expect(map.limited).toEqual([])

    const california: Bounds = [
      [-126.5, 30.5],
      [-112.1, 44.1],
    ]

    limit(california)

    expect(map.limited).toEqual([california])

    // Limits that say the same thing are not handed over again.
    limit([
      [-126.5, 30.5],
      [-112.1, 44.1],
    ])
    expect(map.limited).toHaveLength(1)
  })

  it('puts the pin and its label on the address', () => {
    renderMap()
    const marker = Marker.instances.at(-1)

    expect(marker?.lngLat).toEqual([...lookup.location])
    expect(marker?.element.querySelector('.district-map__pin')).not.toBeNull()
    expect(marker?.element).toHaveTextContent('1420 Maple Street')
  })

  it('zooms half again closer or further with each press', async () => {
    const { map } = renderMap()
    const start = map.getZoom()

    await userEvent.click(screen.getByRole('button', { name: 'Zoom in' }))
    expect(map.getZoom()).toBeCloseTo(start + ZOOM_STEP)

    await userEvent.click(screen.getByRole('button', { name: 'Zoom out' }))
    await userEvent.click(screen.getByRole('button', { name: 'Zoom out' }))
    expect(map.getZoom()).toBeCloseTo(start - ZOOM_STEP)
  })

  // aria-disabled rather than disabled, so a keyboard user is not dropped
  // back to the top of the page when a limit is reached.
  it('marks a zoom button that has reached its limit, and keeps its focus', async () => {
    const { map } = renderMap()
    const zoomIn = screen.getByRole('button', { name: 'Zoom in' })
    const zoomOut = screen.getByRole('button', { name: 'Zoom out' })

    act(() => {
      map.zoomTo(MAX_ZOOM)
    })
    expect(zoomIn).toHaveAttribute('aria-disabled', 'true')

    zoomIn.focus()
    await userEvent.keyboard('{Enter}')
    expect(zoomIn).toHaveFocus()
    expect(map.getZoom()).toBe(MAX_ZOOM)

    act(() => {
      map.zoomTo(MIN_ZOOM)
    })
    expect(zoomOut).toHaveAttribute('aria-disabled', 'true')
    expect(zoomIn).not.toHaveAttribute('aria-disabled')
  })

  it('keeps the scale bar true to the zoom', () => {
    const { map } = renderMap()

    act(() => {
      map.zoomTo(12)
    })
    expect(screen.getByText('1 mi')).toBeInTheDocument()

    act(() => {
      map.zoomTo(15)
    })
    expect(screen.getByText('0.1 mi')).toBeInTheDocument()
  })

  it('redraws in the new colours when the theme changes', async () => {
    const { map } = renderMap()
    const before = map.style

    act(() => {
      document.documentElement.setAttribute('data-theme', 'dark')
    })

    // The observer reports after the attribute change, on a microtask.
    await act(async () => {})

    expect(map.style).not.toBe(before)
    document.documentElement.removeAttribute('data-theme')
  })

  it('says so, and still shows the chrome, when the browser cannot draw a map', () => {
    Map.failNext = true

    render(
      <DistrictMap
        location={lookup.location}
        label={lookup.label}
        layers={layersFor()}
        maxBounds={sampleLimits}
        compact={false}
      />,
    )

    expect(screen.getByText(/can’t be drawn in this browser/)).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { level: 2, name: 'State House · District 44' }),
    ).toBeInTheDocument()
  })
})
