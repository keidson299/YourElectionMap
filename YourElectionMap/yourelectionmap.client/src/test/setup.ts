import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Unmount anything a test rendered so tests stay independent of each other.
afterEach(() => {
  cleanup()
})
