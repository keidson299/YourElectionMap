import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { Map as FakeMap } from './test/fakeMaplibre'
import { sampleLookup } from './test/sampleLookup'
import {
  startFakeDistrictsApi,
  stopFakeDistrictsApi,
  type FakeDistrictsApi,
} from './test/fakeDistrictsApi'

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

/** The districts, once the lookup behind the current page has come back. */
function districtList() {
  return screen.findByRole('radiogroup', { name: '6 districts cover you' })
}

describe('App', () => {
  let api: FakeDistrictsApi

  beforeEach(() => {
    goTo('/')
    window.localStorage.clear()
    document.documentElement.removeAttribute('data-theme')
    api = startFakeDistrictsApi()
  })

  afterEach(stopFakeDistrictsApi)

  it('renders the home screen at the root', () => {
    render(<App />)

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Find My Maps' }),
    ).toBeInTheDocument()
  })

  it('names where the map data comes from before anything is looked up', () => {
    render(<App />)

    expect(
      screen.getByRole('link', { name: 'U.S. Census Bureau' }),
    ).toHaveAttribute(
      'href',
      'https://tigerweb.geo.census.gov/tigerwebmain/TIGERweb_main.html',
    )
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
      await screen.findByRole('heading', {
        level: 1,
        name: 'Districts for 1420 Maple Street',
      }),
    ).toHaveFocus()
    expect(document.title).toBe(
      'Districts for 1420 Maple Street, Springfield — YourElectionMap',
    )
    expect(screen.getByLabelText('Home address')).toHaveValue(
      '1420 Maple Street, Springfield',
    )

    // The lookup ran before the page changed, so the districts are already here.
    expect(await districtList()).toBeInTheDocument()
    expect(screen.queryByText('Looking up districts…')).toBeNull()
    expect(api.fetch).toHaveBeenCalledTimes(1)
  })

  it('stays on the home screen when the address cannot be matched', async () => {
    render(<App />)

    await userEvent.type(screen.getByLabelText('Home address'), 'Main Street')
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(currentUrl()).toBe('/')
    expect(
      screen.getByText(
        'We could not find districts for that address in the fifty states.'
        + ' Check it, or try adding your city or ZIP code.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Home address')).toHaveValue('Main Street')
  })

  it('returns to the home screen when the browser goes back', async () => {
    render(<App />)

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street{enter}',
    )

    expect(await districtList()).toBeInTheDocument()

    act(() => {
      window.history.replaceState(null, '', '/')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })

    expect(
      screen.getByRole('heading', { level: 1, name: 'YourElectionMap' }),
    ).toBeInTheDocument()
  })

  it('says it is looking things up until the districts arrive', async () => {
    let deliver = () => {}

    vi.stubGlobal(
      'fetch',
      () =>
        new Promise((resolve) => {
          deliver = () =>
            resolve({
              ok: true,
              status: 200,
              json: () =>
                Promise.resolve(
                  JSON.parse(
                    JSON.stringify(sampleLookup({ address: '1420 Maple Street' })),
                  ),
                ),
            })
        }),
    )

    goTo('/map?address=1420+Maple+Street')
    render(<App />)

    expect(screen.getByText('Looking up districts…')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Looking up districts for 1420 Maple Street',
      }),
    ).toBeInTheDocument()

    await act(async () => {
      deliver()
    })

    expect(await districtList()).toBeInTheDocument()
    // Nothing else would say the districts are here: the URL never changed, so
    // App's page-change effect does not fire.
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Districts for 1420 Maple Street',
      }),
    ).toHaveFocus()
  })

  it('says a deep-linked address could not be placed', async () => {
    goTo('/map?address=Main+Street')
    render(<App />)

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'We could not place that address',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /covers the fifty states\. Check the address/,
      ),
    ).toBeInTheDocument()
    expect(screen.queryByRole('radio')).toBeNull()
    // Not a failure, so there is nothing to keep trying.
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull()
  })

  it('offers another go when the lookup does not answer, and hands focus to the result', async () => {
    let deliver = () => {}

    api.fail()
    goTo('/map?address=1420+Maple+Street')
    render(<App />)

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'The district lookup is not responding',
      }),
    ).toBeInTheDocument()

    const retry = screen.getByRole('button', { name: 'Try again' })

    // A held answer, so the button can be looked at mid-retry.
    vi.stubGlobal(
      'fetch',
      () =>
        new Promise((resolve) => {
          deliver = () =>
            resolve({
              ok: true,
              status: 200,
              json: () =>
                Promise.resolve(
                  JSON.parse(
                    JSON.stringify(sampleLookup({ address: '1420 Maple Street' })),
                  ),
                ),
            })
        }),
    )

    await userEvent.click(retry)

    const trying = screen.getByRole('button', { name: 'Trying again…' })

    expect(trying).toHaveFocus()
    expect(trying).toHaveAttribute('aria-disabled', 'true')

    await act(async () => {
      deliver()
    })

    expect(await districtList()).toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'Districts for 1420 Maple Street',
      }),
    ).toHaveFocus()
  })

  it('records a picked district in the URL without adding to history', async () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)
    await districtList()

    const entriesBefore = window.history.length
    const county = screen.getByRole('radio', {
      name: 'County Commission · District 3',
    })

    await userEvent.click(county)

    expect(currentUrl()).toBe('/map?address=1420+Maple+Street&district=county-3')
    expect(window.history.length).toBe(entriesBefore)
    // Same page, so focus stays in the list rather than jumping to the heading.
    expect(county).toHaveFocus()
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

  it('opens a deep link on the district it names', async () => {
    goTo('/map?address=1420+Maple+Street&district=county-3')
    render(<App />)
    await districtList()

    expect(checkedDistrict()).toHaveAccessibleName(
      'County Commission · District 3',
    )
  })

  it('drops the selected district when a new address is looked up', async () => {
    goTo('/map?address=1420+Maple+Street&district=county-3')
    render(<App />)
    await districtList()

    const field = screen.getByLabelText('Home address')
    await userEvent.clear(field)
    await userEvent.type(field, '88 Harbor Road')
    await userEvent.click(screen.getByRole('button', { name: 'Update' }))

    expect(currentUrl()).toBe('/map?address=88+Harbor+Road')
    expect(checkedDistrict()).toHaveAccessibleName('State House · District 44')
  })

  it('keeps the map it is showing when a new address cannot be matched', async () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)
    await districtList()

    const field = screen.getByLabelText('Home address')
    await userEvent.clear(field)
    await userEvent.type(field, 'Main Street')
    await userEvent.click(screen.getByRole('button', { name: 'Update' }))

    expect(currentUrl()).toBe('/map?address=1420+Maple+Street')
    expect(
      screen.getByText(/We could not find districts for that address/),
    ).toBeInTheDocument()
    expect(await districtList()).toBeInTheDocument()
  })

  it('opens a new lookup on its fitted view, not the last zoom', async () => {
    goTo('/map?address=1420+Maple+Street')
    render(<App />)
    await districtList()

    await userEvent.click(await screen.findByRole('button', { name: 'Zoom in' }))
    const first = FakeMap.instances.at(-1)
    expect(first?.getZoom()).toBeGreaterThan(12)

    const field = screen.getByLabelText('Home address')
    await userEvent.clear(field)
    await userEvent.type(field, '88 Harbor Road')
    await userEvent.click(screen.getByRole('button', { name: 'Update' }))
    await screen.findByRole('button', { name: 'Zoom in' })

    // A fresh map, opened on its own fitted view; the zoomed one is gone.
    const latest = FakeMap.instances.at(-1)
    expect(latest).not.toBe(first)
    expect(first?.removed).toBe(true)
    expect(latest?.getZoom()).toBe(12)
    expect(latest?.options.bounds).toBeDefined()
  })
})
