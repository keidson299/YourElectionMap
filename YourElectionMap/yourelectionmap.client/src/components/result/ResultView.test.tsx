import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ResultView } from './ResultView'
import { sampleLookup } from '../../districts/sampleLookup'

// jsdom has no matchMedia, so the result view renders its desktop layout by
// default. These tests stand in a phone-sized window.

function stubPhoneWindow() {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string) => ({
      matches: true,
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }),
  })
}

const lookup = sampleLookup({ address: '1420 Maple Street, Springfield' })

describe('ResultView on a phone', () => {
  beforeEach(stubPhoneWindow)

  afterEach(() => {
    Reflect.deleteProperty(window, 'matchMedia')
  })

  it('names the map with the short form and swaps the legend for a count', () => {
    render(
      <ResultView lookup={lookup} requestedDistrict={null} onSelect={() => {}} />,
    )

    expect(
      screen.getByRole('heading', { level: 2, name: 'State House · 44' }),
    ).toBeInTheDocument()
    expect(screen.getByText('3 boundaries')).toBeInTheDocument()
    expect(screen.queryByText('On this map')).toBeNull()
    expect(screen.getByText('Tap to draw')).toBeInTheDocument()
  })

  it('expands the sheet from its handle and collapses it on a pick', async () => {
    const onSelect = vi.fn()

    render(
      <ResultView lookup={lookup} requestedDistrict={null} onSelect={onSelect} />,
    )

    const handle = screen.getByRole('button', { name: 'District list' })
    expect(handle).toHaveAttribute('aria-expanded', 'false')

    await userEvent.click(handle)
    expect(handle).toHaveAttribute('aria-expanded', 'true')

    await userEvent.click(
      screen.getByRole('radio', { name: 'School Board · Zone 5' }),
    )
    expect(onSelect).toHaveBeenCalledWith('school-5')
    expect(handle).toHaveAttribute('aria-expanded', 'false')
  })
})

describe('ResultView on a desktop', () => {
  it('names the page after the address for assistive technology', () => {
    render(
      <ResultView lookup={lookup} requestedDistrict={null} onSelect={() => {}} />,
    )

    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Districts for 1420 Maple Street',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'District list' })).toBeNull()
  })
})
