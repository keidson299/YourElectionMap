import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AddressForm } from './AddressForm'

const originalGeolocation = Object.getOwnPropertyDescriptor(
  navigator,
  'geolocation',
)

function renderForm() {
  const onSubmit = vi.fn()
  const onLocate = vi.fn()

  const { unmount } = render(
    <AddressForm
      variant="home"
      submitLabel="Find My Maps"
      onSubmit={onSubmit}
      onLocate={onLocate}
    />,
  )

  return { onSubmit, onLocate, unmount }
}

function stubGeolocation(getCurrentPosition: unknown) {
  Object.defineProperty(navigator, 'geolocation', {
    value: { getCurrentPosition },
    configurable: true,
  })
}

afterEach(() => {
  if (originalGeolocation) {
    Object.defineProperty(navigator, 'geolocation', originalGeolocation)
  } else {
    Reflect.deleteProperty(navigator, 'geolocation')
  }
})

describe('AddressForm', () => {
  it('labels the field for assistive technology without showing the label', () => {
    renderForm()

    const field = screen.getByLabelText('Home address')

    expect(field).toBeInTheDocument()
    expect(screen.getByText('Home address')).toHaveClass('visually-hidden')
  })

  it('hands the trimmed address to the caller', async () => {
    const { onSubmit } = renderForm()

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '  1420 Maple Street  ',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(onSubmit).toHaveBeenCalledWith('1420 Maple Street')
  })

  it('submits when Enter is pressed in the field', async () => {
    const { onSubmit } = renderForm()

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street{enter}',
    )

    expect(onSubmit).toHaveBeenCalledWith('1420 Maple Street')
  })

  it('refuses an empty submit and says why', async () => {
    const { onSubmit } = renderForm()

    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByRole('status')).toHaveTextContent(
      'Enter an address to continue.',
    )
    expect(screen.getByLabelText('Home address')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('stops flagging the field once something is typed', async () => {
    renderForm()

    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))
    await userEvent.type(screen.getByLabelText('Home address'), '1')

    expect(screen.getByLabelText('Home address')).not.toHaveAttribute(
      'aria-invalid',
    )
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('hands over the coordinates the browser reports', async () => {
    stubGeolocation((success: (position: unknown) => void) => {
      success({ coords: { latitude: 42.1, longitude: -71.2 } })
    })

    const { onLocate } = renderForm()

    await userEvent.click(
      screen.getByRole('button', { name: /use my current location/i }),
    )

    expect(onLocate).toHaveBeenCalledWith({ lat: 42.1, lon: -71.2 })
  })

  it('explains when the browser will not share a location', async () => {
    stubGeolocation((_success: unknown, failure: () => void) => {
      failure()
    })

    const { onLocate } = renderForm()

    await userEvent.click(
      screen.getByRole('button', { name: /use my current location/i }),
    )

    expect(onLocate).not.toHaveBeenCalled()
    expect(screen.getByRole('status')).toHaveTextContent(
      'We could not get your location.',
    )
    // The address field did nothing wrong.
    expect(screen.getByLabelText('Home address')).not.toHaveAttribute(
      'aria-invalid',
    )
  })

  it('ignores a location that arrives after the form has gone', async () => {
    let succeed: (position: unknown) => void = () => {}
    stubGeolocation((success: (position: unknown) => void) => {
      succeed = success
    })

    const { onLocate, unmount } = renderForm()
    const locate = screen.getByRole('button', {
      name: /use my current location/i,
    })

    await userEvent.click(locate)
    expect(locate).toHaveAttribute('aria-disabled', 'true')
    expect(locate).toHaveFocus()

    unmount()
    succeed({ coords: { latitude: 42.1, longitude: -71.2 } })

    expect(onLocate).not.toHaveBeenCalled()
  })

  it('explains when the browser has no geolocation at all', async () => {
    Reflect.deleteProperty(navigator, 'geolocation')

    const { onLocate } = renderForm()

    await userEvent.click(
      screen.getByRole('button', { name: /use my current location/i }),
    )

    expect(onLocate).not.toHaveBeenCalled()
    expect(screen.getByRole('status')).toHaveTextContent(
      'This browser cannot share your location.',
    )
  })
})
