import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import App from './App'

function goTo(url: string) {
  window.history.replaceState(null, '', url)
}

function currentUrl() {
  return `${window.location.pathname}${window.location.search}`
}

describe('App', () => {
  beforeEach(() => {
    goTo('/')
    window.localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
  })

  it('renders the home screen at the root', () => {
    render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Find My Maps' }),
    ).toBeInTheDocument()
  })

  it('sends a submitted address to the map URL and echoes it back', async () => {
    render(<App />)

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street, Springfield',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(currentUrl()).toBe('/map?address=1420+Maple+Street%2C+Springfield')
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: '1420 Maple Street, Springfield',
      }),
    ).toBeInTheDocument()
  })

  it('returns to the home screen when the browser goes back', async () => {
    render(<App />)

    await userEvent.type(screen.getByLabelText('Home address'), '1420 Maple Street')
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(
      screen.getByRole('heading', { level: 1, name: '1420 Maple Street' }),
    ).toBeInTheDocument()

    act(() => {
      window.history.replaceState(null, '', '/')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
  })

  it('renders the map shell for a deep link', () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: '1420 Maple Street' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Update' })).toBeInTheDocument()
  })

  it('falls back to the home screen for an unknown path', () => {
    goTo('/somewhere-else')
    render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
  })

  it('falls back to the home screen when the map URL has nothing to look up', () => {
    goTo('/map')
    render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
  })
})
