// Stands in for maplibre-gl in every test (see setup.ts): jsdom has no WebGL, so
// the real map cannot even be constructed. The fake keeps just enough state to
// answer what DistrictMap asks — its zoom and centre — and records what it was
// told, so tests can check the style, the view and the pin.

type Listener = (event?: unknown) => void

/** Where the real library would load its worker from. */
export let workerUrl: string | null = null

export function setWorkerUrl(url: string) {
  workerUrl = url
}
type Bounds = [[number, number], [number, number]]

export class Map {
  /** Every map made since the last reset, newest last. */
  static instances: Map[] = []

  /** Set to make the next construction throw, as MapLibre does without WebGL. */
  static failNext = false

  static reset() {
    Map.instances = []
    Map.failNext = false
  }

  options: Record<string, unknown>
  style: unknown
  fitted: { bounds: Bounds; options?: unknown }[] = []
  removed = false
  images = new Set<string>()

  private zoom: number
  private center: { lng: number; lat: number }
  private listeners = new globalThis.Map<string, Listener[]>()

  readonly touchZoomRotate = { disableRotation() {} }
  readonly keyboard = { disableRotation() {} }

  constructor(options: Record<string, unknown>) {
    if (Map.failNext) {
      Map.failNext = false
      throw new Error('Failed to initialize WebGL')
    }

    this.options = options
    this.style = options.style
    this.zoom = 12
    this.center = centreOf(options.bounds as Bounds)
    Map.instances.push(this)
  }

  on(type: string, listener: Listener) {
    this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener])
    return this
  }

  /** Lets a test fire an event the real map would. */
  fire(type: string, event?: unknown) {
    for (const listener of this.listeners.get(type) ?? []) {
      listener(event)
    }
  }

  getZoom() {
    return this.zoom
  }

  getCenter() {
    return this.center
  }

  zoomTo(zoom: number) {
    this.zoom = zoom
    this.fire('move')
    return this
  }

  fitBounds(bounds: Bounds, options?: unknown) {
    this.fitted.push({ bounds, options })
    this.center = centreOf(bounds)
    this.fire('move')
    return this
  }

  setStyle(style: unknown) {
    this.style = style
    return this
  }

  hasImage(id: string) {
    return this.images.has(id)
  }

  addImage(id: string) {
    this.images.add(id)
    return this
  }

  remove() {
    this.removed = true
  }
}

export class Marker {
  static instances: Marker[] = []

  element: HTMLElement
  lngLat: [number, number] | null = null
  map: Map | null = null

  constructor(options: { element: HTMLElement }) {
    this.element = options.element
    Marker.instances.push(this)
  }

  setLngLat(lngLat: [number, number]) {
    this.lngLat = lngLat
    return this
  }

  addTo(map: Map) {
    this.map = map
    // The real marker puts its element into the map's container.
    ;(map.options.container as HTMLElement).append(this.element)
    return this
  }

  remove() {
    this.element.remove()
    this.map = null
    return this
  }
}

function centreOf(bounds: Bounds | undefined): { lng: number; lat: number } {
  if (bounds === undefined) {
    return { lng: 0, lat: 0 }
  }

  const [[west, south], [east, north]] = bounds

  return { lng: (west + east) / 2, lat: (south + north) / 2 }
}
