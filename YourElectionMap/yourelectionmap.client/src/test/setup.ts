import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { Map, Marker } from './fakeMaplibre'

// jsdom has no WebGL, so the street map library is replaced everywhere by a
// fake that records what it is told. See fakeMaplibre.ts.
vi.mock('maplibre-gl', () => import('./fakeMaplibre'))

// Unmount anything a test rendered so tests stay independent of each other.
afterEach(() => {
  cleanup()
  Map.reset()
  Marker.instances = []
})
