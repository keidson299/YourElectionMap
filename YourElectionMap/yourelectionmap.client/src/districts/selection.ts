import type { District, DistrictLevel } from './types'

/** Drawn at neighbourhood scale, and reliably available from the Census. */
const DEFAULT_LEVEL: DistrictLevel = 'state-lower'

/**
 * The district named in the URL when the lookup has it, otherwise the lower
 * state chamber, otherwise the first district. A lookup is never empty: the
 * data layer reads one with no districts as no match.
 */
export function resolveSelection(
  districts: readonly District[],
  requestedId: string | null,
): District {
  return (
    districts.find((district) => district.id === requestedId) ??
    districts.find((district) => district.level === DEFAULT_LEVEL) ??
    districts[0]
  )
}

/** What the map draws: the selection solid, and up to two neighbours for context. */
export type MapLayers = {
  selected: District
  dashed: District | null
  dotted: District | null
}

export type LayerRole = 'selected' | 'dashed' | 'dotted' | 'none'

/**
 * The context districts are the selection's nearest neighbours in list order,
 * one on each side where the list allows. The earlier one is dashed and the
 * later one dotted, so at either end of the list both come from one side.
 */
export function mapLayers(
  districts: readonly District[],
  selected: District,
): MapLayers {
  const index = districts.indexOf(selected)
  const context: number[] = []

  for (
    let distance = 1;
    index !== -1 && context.length < 2 && distance < districts.length;
    distance += 1
  ) {
    if (index - distance >= 0) {
      context.push(index - distance)
    }

    if (context.length < 2 && index + distance < districts.length) {
      context.push(index + distance)
    }
  }

  const [dashed, dotted] = context.sort((a, b) => a - b)

  return {
    selected,
    dashed: dashed === undefined ? null : districts[dashed],
    dotted: dotted === undefined ? null : districts[dotted],
  }
}

export function layerRole(layers: MapLayers, district: District): LayerRole {
  if (district === layers.selected) {
    return 'selected'
  }

  if (district === layers.dashed) {
    return 'dashed'
  }

  return district === layers.dotted ? 'dotted' : 'none'
}

/** How many boundaries the map is drawing. */
export function layerCount(layers: MapLayers): number {
  return 1 + (layers.dashed === null ? 0 : 1) + (layers.dotted === null ? 0 : 1)
}
