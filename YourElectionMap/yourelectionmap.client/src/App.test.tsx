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

function checkedDistrict() {
  return screen
    .getAllByRole('radio')
    .find((radio) => (radio as HTMLInputElement).checked)
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

  it('sends a submitted address to the result page', async () => {
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
        name: 'Districts for 1420 Maple Street',
      }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Home address')).toHaveValue(
      '1420 Maple Street, Springfield',
    )
  })

  it('returns to the home screen when the browser goes back', async () => {
    render(<App />)

    await userEvent.type(screen.getByLabelText('Home address'), '1420 Maple Street')
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(
      screen.getByRole('radiogroup', { name: '6 districts cover you' }),
    ).toBeInTheDocument()

    act(() => {
      window.history.replaceState(null, '', '/')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
  })

  it('opens a deep link on the lower state chamber', () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)

    expect(screen.getAllByRole('radio')).toHaveLength(6)
    expect(checkedDistrict()).toHaveAccessibleName('State House · District 44')
    expect(screen.getByRole('button', { name: 'Update' })).toBeInTheDocument()
  })

  it('records a picked district in the URL without adding to history', async () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)

    const entriesBefore = window.history.length

    await userEvent.click(
      screen.getByRole('radio', { name: 'County Commission · District 3' }),
    )

    expect(currentUrl()).toBe('/map?address=1420+Maple+Street&district=county-3')
    expect(window.history.length).toBe(entriesBefore)
    expect(checkedDistrict()).toHaveAccessibleName(
      'County Commission · District 3',
    )
    expect(
      screen.getByRole('heading', {
        level: 2,
        name: 'County Commission · District 3',
      }),
    ).toBeInTheDocument()
  })

  it('opens a deep link on the district it names', () => {
    goTo('/map?address=1420+Maple+Street&district=county-3')
    render(<App />)

    expect(checkedDistrict()).toHaveAccessibleName(
      'County Commission · District 3',
    )
  })

  it('falls back to the lower state chamber for a district it does not have', () => {
    goTo('/map?address=1420+Maple+Street&district=nowhere-9')
    render(<App />)

    expect(checkedDistrict()).toHaveAccessibleName('State House · District 44')
  })

  it('drops the selected district when a new address is looked up', async () => {
    goTo('/map?address=1420+Maple+Street&district=county-3')
    render(<App />)

    const field = screen.getByLabelText('Home address')
    await userEvent.clear(field)
    await userEvent.type(field, '88 Harbor Road')
    await userEvent.click(screen.getByRole('button', { name: 'Update' }))

    expect(currentUrl()).toBe('/map?address=88+Harbor+Road')
    expect(checkedDistrict()).toHaveAccessibleName('State House · District 44')
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
