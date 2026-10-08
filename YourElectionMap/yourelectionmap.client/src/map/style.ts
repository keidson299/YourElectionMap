import type { MapOptions } from 'maplibre-gl'
import type { MapLayers } from '../districts/selection'
import type { District } from '../districts/types'

// The result map's whole look, built here rather than fetched: streets and
// their names from OpenFreeMap's tiles, and nothing else of theirs, under the
// districts drawn the way the spec draws them. Rebuilt whenever the selection
// or the theme changes; MapLibre works out what actually changed.

export type StyleSpecification = Exclude<MapOptions['style'], string | undefined>
type LayerSpecification = StyleSpecification['layers'][number]

/**
 * OpenFreeMap: OpenStreetMap streets as vector tiles, free, with no key, no
 * account and no cookies. The visitor's browser asks it for tiles directly,
 * so it sees roughly where on the map they are looking. Swap the provider here.
 */
export const TILE_SOURCE = {
  url: 'https://tiles.openfreemap.org/planet',
  glyphs: 'https://tiles.openfreemap.org/fonts/{fontstack}/{range}.pbf',
  font: 'Noto Sans Regular',
}

/** Colours from the page's tokens, resolved to values MapLibre can read. */
export type MapColors = {
  paper: string
  road: string
  roadMajor: string
  label: string
  accent: string
  accentTint: string
}

const TOKENS: Record<keyof MapColors, string> = {
  paper: '--bg',
  road: '--road',
  roadMajor: '--district',
  label: '--ink-soft',
  accent: '--accent',
  accentTint: '--accent-tint',
}

/**
 * The current theme's colours. Each token is resolved through a probe element,
 * so `var()` chains, the dark set and the newer `rgb(r g b / a)` syntax all come
 * back as the plain `rgb()` and `rgba()` MapLibre parses.
 */
export function readMapColors(): MapColors {
  const probe = document.createElement('span')
  probe.style.display = 'none'
  document.body.append(probe)

  try {
    const colors = {} as MapColors

    for (const key of Object.keys(TOKENS) as (keyof MapColors)[]) {
      probe.style.color = ''
      probe.style.color = `var(${TOKENS[key]})`
      colors[key] = getComputedStyle(probe).color || FALLBACK
    }

    return colors
  } finally {
    probe.remove()
  }
}

// Only seen where styles are not computed at all, as in the tests.
const FALLBACK = 'rgb(128, 128, 128)'

/** Line widths and dash patterns, in CSS pixels, from the spec's Result screens. */
const LINES = {
  desktop: {
    selected: 3.4,
    context: 2.4,
    contextOpacity: 0.68,
    dashed: [9, 6],
    dotted: [1.5, 6],
    hatchSpacing: 57,
  },
  phone: {
    selected: 3,
    context: 2,
    contextOpacity: 0.65,
    dashed: [8, 5],
    dotted: [1.5, 5],
    hatchSpacing: 33,
  },
}

/** The id the hatch image is registered under; it carries what draws it. */
export function hatchId(spacing: number, color: string): string {
  return `hatch|${spacing}|${color}`
}

/** The spacing and colour a hatch id was made from, or null for any other image. */
export function readHatchId(
  id: string,
): { spacing: number; color: string } | null {
  const [prefix, spacing, color] = id.split('|')

  return prefix === 'hatch' && spacing && color
    ? { spacing: Number(spacing), color }
    : null
}

export function buildStyle({
  layers,
  colors,
  compact,
}: {
  layers: MapLayers
  colors: MapColors
  compact: boolean
}): StyleSpecification {
  const lines = LINES[compact ? 'phone' : 'desktop']

  // MapLibre measures dashes in line widths, the spec in pixels.
  const inWidths = (pattern: number[]) =>
    pattern.map((length) => length / lines.context)

  return {
    version: 8,
    glyphs: TILE_SOURCE.glyphs,
    sources: {
      streets: { type: 'vector', url: TILE_SOURCE.url },
      selected: districtSource(layers.selected),
      dashed: districtSource(layers.dashed),
      dotted: districtSource(layers.dotted),
    },
    layers: [
      {
        id: 'paper',
        type: 'background',
        paint: { 'background-color': colors.paper },
      },
      ...streetLayers(colors),
      {
        id: 'selected-fill',
        type: 'fill',
        source: 'selected',
        paint: { 'fill-color': colors.accentTint },
      },
      {
        id: 'selected-hatch',
        type: 'fill',
        source: 'selected',
        paint: {
          'fill-pattern': hatchId(lines.hatchSpacing, colors.accent),
        },
      },
      {
        id: 'street-names',
        type: 'symbol',
        source: 'streets',
        'source-layer': 'transportation_name',
        minzoom: 13,
        layout: {
          'symbol-placement': 'line',
          'text-field': ['get', 'name'],
          'text-font': [TILE_SOURCE.font],
          'text-size': ['interpolate', ['linear'], ['zoom'], 13, 10.5, 17, 13],
        },
        paint: {
          'text-color': colors.label,
          'text-halo-color': colors.paper,
          'text-halo-width': 1.5,
        },
      },
      {
        id: 'dashed-line',
        type: 'line',
        source: 'dashed',
        layout: { 'line-join': 'round' },
        paint: {
          'line-color': colors.accent,
          'line-width': lines.context,
          'line-opacity': lines.contextOpacity,
          'line-dasharray': inWidths(lines.dashed),
        },
      },
      {
        id: 'dotted-line',
        type: 'line',
        source: 'dotted',
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: {
          'line-color': colors.accent,
          'line-width': lines.context,
          'line-opacity': lines.contextOpacity,
          'line-dasharray': inWidths(lines.dotted),
        },
      },
      {
        id: 'selected-line',
        type: 'line',
        source: 'selected',
        layout: { 'line-join': 'round' },
        paint: {
          'line-color': colors.accent,
          'line-width': lines.selected,
        },
      },
    ],
  }
}

/** One district as a GeoJSON source, or an empty one where there is no neighbour. */
function districtSource(
  district: District | null,
): StyleSpecification['sources'][string] {
  return {
    type: 'geojson',
    data: {
      type: 'FeatureCollection',
      features:
        district === null
          ? []
          : [
              {
                type: 'Feature',
                properties: { id: district.id },
                geometry: {
                  type: 'MultiPolygon',
                  // The contract's arrays are read-only; GeoJSON's types are not.
                  coordinates: district.boundary as unknown as number[][][][],
                },
              },
            ],
    },
  }
}

/**
 * The streets, in three weights by the class OpenMapTiles gives them. The tiles
 * decide which classes exist at which zoom — highways from the state view,
 * local streets only close in — so the map thins itself as it widens.
 */
function streetLayers(colors: MapColors): LayerSpecification[] {
  const street = (
    id: string,
    classes: string[],
    color: string,
    widths: number[],
  ): LayerSpecification => ({
    id,
    type: 'line',
    source: 'streets',
    'source-layer': 'transportation',
    filter: ['in', ['get', 'class'], ['literal', classes]],
    layout: { 'line-join': 'round', 'line-cap': 'round' },
    paint: {
      'line-color': color,
      'line-width': ['interpolate', ['exponential', 1.6], ['zoom'], ...widths],
    },
  })

  return [
    street('streets-minor', ['minor', 'service'], colors.road, [
      12, 0.5, 14, 1.4, 17, 6,
    ]),
    street('streets-middle', ['secondary', 'tertiary'], colors.road, [
      8, 0.5, 12, 1.4, 14, 2.6, 17, 9,
    ]),
    street('streets-major', ['motorway', 'trunk', 'primary'], colors.roadMajor, [
      5, 0.6, 10, 1.5, 14, 3.6, 17, 12,
    ]),
  ]
}
