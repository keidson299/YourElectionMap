import type { District } from './types'

/**
 * The district named in the URL if the lookup has it, else the lower state
 * chamber, else the upper (Nebraska has one), else the first district.
 */
export function resolveSelection(
  districts: readonly District[],
  requestedId: string | null,
): District {
  return (
    districts.find((district) => district.id === requestedId) ??
    districts.find((district) => district.level === 'state-lower') ??
    districts.find((district) => district.level === 'state-upper') ??
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
 * The context is the selection's nearest neighbours in list order: one each
 * side, or both from one side at either end. The earlier one is dashed.
 */
export function mapLayers(
  districts: readonly District[],
  selected: District,
): MapLayers {
  const index = districts.indexOf(selected)
  const start = Math.max(0, Math.min(index - 1, districts.length - 3))
  const [dashed = null, dotted = null] = districts
    .slice(start, start + 3)
    .filter((district) => district !== selected)

  return { selected, dashed, dotted }
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
