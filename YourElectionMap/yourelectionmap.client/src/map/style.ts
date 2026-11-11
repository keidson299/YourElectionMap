import type { MapOptions } from 'maplibre-gl'
import type { MapLayers } from '../districts/selection'
import type { District } from '../districts/types'

// The result map's whole look, built here rather than fetched: from
// OpenFreeMap's tiles, the streets and what a visitor finds their bearings by
// (towns, water, parks, highway numbers), under the districts drawn the way
// the spec draws them. Rebuilt whenever the selection or the theme changes;
// MapLibre works out what actually changed.

export type StyleSpecification = Exclude<MapOptions['style'], string | undefined>
type LayerSpecification = StyleSpecification['layers'][number]
type SymbolLayer = Extract<LayerSpecification, { type: 'symbol' }>

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
  placeLabel: string
  water: string
  waterLabel: string
  park: string
  accent: string
  accentTint: string
}

const TOKENS: Record<keyof MapColors, string> = {
  paper: '--bg',
  road: '--road',
  roadMajor: '--district',
  label: '--ink-soft',
  placeLabel: '--ink',
  water: '--water',
  waterLabel: '--water-ink',
  park: '--park',
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

  // A neighbour's boundary, fainter than the selection's. MapLibre measures
  // dashes in line widths, the spec in pixels; a dot is a short dash with
  // round caps.
  const contextLine = (role: 'dashed' | 'dotted'): LayerSpecification => ({
    id: `${role}-line`,
    type: 'line',
    source: role,
    layout:
      role === 'dotted'
        ? { 'line-join': 'round', 'line-cap': 'round' }
        : { 'line-join': 'round' },
    paint: {
      'line-color': colors.accent,
      'line-width': lines.context,
      'line-opacity': lines.contextOpacity,
      'line-dasharray': lines[role].map((length) => length / lines.context),
    },
  })

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
      ...groundLayers(colors),
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
      ...labelLayers(colors),
      contextLine('dashed'),
      contextLine('dotted'),
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
 * What lies under the streets: parks and other public land, a town's parks,
 * golf courses and greens, and water. All of it pale, so the hatched district
 * stays the strongest thing on the map.
 */
function groundLayers(colors: MapColors): LayerSpecification[] {
  return [
    {
      id: 'parks',
      type: 'fill',
      source: 'streets',
      'source-layer': 'park',
      paint: { 'fill-color': colors.park },
    },
    {
      id: 'green-space',
      type: 'fill',
      source: 'streets',
      'source-layer': 'landcover',
      filter: [
        'in',
        ['get', 'subclass'],
        ['literal', ['park', 'garden', 'golf_course', 'recreation_ground', 'village_green']],
      ],
      paint: { 'fill-color': colors.park },
    },
    {
      id: 'water',
      type: 'fill',
      source: 'streets',
      'source-layer': 'water',
      filter: ['!=', ['get', 'class'], 'swimming_pool'],
      paint: { 'fill-color': colors.water },
    },
    {
      id: 'waterways',
      type: 'line',
      source: 'streets',
      'source-layer': 'waterway',
      filter: ['in', ['get', 'class'], ['literal', ['river', 'canal']]],
      layout: { 'line-join': 'round', 'line-cap': 'round' },
      paint: {
        'line-color': colors.waterLabel,
        'line-opacity': 0.35,
        'line-width': ['interpolate', ['exponential', 1.4], ['zoom'], 8, 0.6, 12, 1.4, 17, 4],
      },
    },
  ]
}

/**
 * The map's names. MapLibre places the labels of higher layers first, so these
 * run from least to most wanted: water and parks, then streets, highway
 * numbers, neighbourhoods, and towns, which win any collision.
 */
function labelLayers(colors: MapColors): LayerSpecification[] {
  // A name from the street tiles, in the tiles' font, haloed in paper unless
  // the layer says otherwise.
  const label = (
    id: string,
    sourceLayer: string,
    color: string,
    layout: SymbolLayer['layout'],
    { paint, ...rest }: Pick<SymbolLayer, 'minzoom' | 'filter' | 'paint'> = {},
  ): SymbolLayer => ({
    id,
    type: 'symbol',
    source: 'streets',
    'source-layer': sourceLayer,
    ...rest,
    layout: {
      'text-field': ['get', 'name'],
      'text-font': [TILE_SOURCE.font],
      ...layout,
    },
    paint: {
      'text-color': color,
      'text-halo-color': colors.paper,
      'text-halo-width': 1.5,
      ...paint,
    },
  })

  return [
    label('water-names', 'water_name', colors.waterLabel, {
      'text-size': ['interpolate', ['linear'], ['zoom'], 8, 10.5, 15, 13],
      'text-letter-spacing': 0.05,
      'text-max-width': 8,
    }),
    label(
      'river-names',
      'waterway',
      colors.waterLabel,
      {
        'symbol-placement': 'line',
        'text-size': ['interpolate', ['linear'], ['zoom'], 11, 10.5, 17, 13],
        'text-letter-spacing': 0.05,
      },
      { minzoom: 11, filter: ['==', ['get', 'class'], 'river'] },
    ),
    // The park layer's named points, one per park, rather than its shapes.
    label(
      'park-names',
      'park',
      colors.label,
      {
        'text-size': ['interpolate', ['linear'], ['zoom'], 9, 10.5, 15, 12.5],
        'text-max-width': 8,
        'symbol-sort-key': ['get', 'rank'],
      },
      { minzoom: 9, filter: ['==', ['geometry-type'], 'Point'] },
    ),
    label(
      'street-names',
      'transportation_name',
      colors.label,
      {
        'symbol-placement': 'line',
        'text-size': ['interpolate', ['linear'], ['zoom'], 13, 10.5, 17, 13],
      },
      { minzoom: 13 },
    ),
    label(
      'highway-numbers',
      'transportation_name',
      colors.placeLabel,
      {
        'symbol-placement': 'line',
        'symbol-spacing': 400,
        // As people say it: "I-5", "US 93". Where the tiles give a bare number
        // the network names the kind of road; a ref that already says it
        // ("I 5"), and a state route, are shown as they are.
        'text-field': [
          'case',
          [
            'all',
            ['in', ['slice', ['get', 'ref'], 0, 1], ['literal', DIGITS]],
            ['==', ['get', 'network'], 'us-interstate'],
          ],
          ['concat', 'I-', ['get', 'ref']],
          [
            'all',
            ['in', ['slice', ['get', 'ref'], 0, 1], ['literal', DIGITS]],
            ['==', ['get', 'network'], 'us-highway'],
          ],
          ['concat', 'US ', ['get', 'ref']],
          ['get', 'ref'],
        ],
        'text-size': ['interpolate', ['linear'], ['zoom'], 6, 10, 14, 12],
        'text-letter-spacing': 0.05,
      },
      {
        filter: [
          'all',
          ['has', 'ref'],
          ['in', ['get', 'class'], ['literal', ['motorway', 'trunk', 'primary']]],
        ],
        paint: { 'text-halo-color': colors.roadMajor, 'text-halo-width': 2.5 },
      },
    ),
    label(
      'neighbourhood-names',
      'place',
      colors.label,
      {
        'text-size': ['interpolate', ['linear'], ['zoom'], 10, 10.5, 15, 13],
        'text-max-width': 8,
        'symbol-sort-key': ['get', 'rank'],
      },
      {
        minzoom: 10,
        filter: [
          'in',
          ['get', 'class'],
          ['literal', ['village', 'hamlet', 'suburb', 'quarter', 'neighbourhood']],
        ],
      },
    ),
    label(
      'town-names',
      'place',
      colors.placeLabel,
      {
        'text-size': [
          'interpolate',
          ['linear'],
          ['zoom'],
          6,
          ['match', ['get', 'class'], 'city', 12.5, 10.5],
          12,
          ['match', ['get', 'class'], 'city', 16, 13.5],
        ],
        'text-transform': 'uppercase',
        'text-letter-spacing': 0.1,
        'text-max-width': 8,
        'symbol-sort-key': ['get', 'rank'],
      },
      {
        filter: ['in', ['get', 'class'], ['literal', ['city', 'town']]],
        paint: { 'text-halo-width': 2 },
      },
    ),
  ]
}

const DIGITS = [...'0123456789']

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
