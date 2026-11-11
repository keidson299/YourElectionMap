import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AddressForm } from './AddressForm'

const originalGeolocation = Object.getOwnPropertyDescriptor(
  navigator,
  'geolocation',
)

/** The caller's lookup answers with `notice`: null when the page moved on. */
function renderForm(notice: { text: string; field: boolean } | null = null) {
  const onSubmit = vi.fn().mockResolvedValue(notice)
  const onLocate = vi.fn().mockResolvedValue(notice)

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

  it('hands the trimmed address to the caller when Enter is pressed', async () => {
    const { onSubmit } = renderForm()

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '  1420 Maple Street  {enter}',
    )

    expect(onSubmit).toHaveBeenCalledWith('1420 Maple Street')
  })

  it('refuses an empty submit, says why, and stops flagging the field once something is typed', async () => {
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

    await userEvent.type(screen.getByLabelText('Home address'), '1')

    expect(screen.getByLabelText('Home address')).not.toHaveAttribute(
      'aria-invalid',
    )
    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('says it is checking, and holds the field, until the lookup answers', async () => {
    let answer: (notice: null) => void = () => {}
    const onSubmit = vi.fn(
      () =>
        new Promise<null>((resolve) => {
          answer = resolve
        }),
    )

    render(
      <AddressForm
        variant="home"
        submitLabel="Find My Maps"
        onSubmit={onSubmit}
      />,
    )

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    const checking = screen.getByRole('button', { name: 'Checking…' })

    expect(checking).toHaveAttribute('aria-disabled', 'true')
    expect(screen.getByLabelText('Home address')).toHaveAttribute('readonly')

    // Pressing again while it waits would look up the same address twice.
    await userEvent.click(checking)
    expect(onSubmit).toHaveBeenCalledTimes(1)

    await act(async () => {
      answer(null)
    })

    expect(
      screen.getByRole('button', { name: 'Find My Maps' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Home address')).not.toHaveAttribute('readonly')
  })

  it('says what the lookup said, and flags the field it was about', async () => {
    renderForm({
      text: 'We could not match that address. Try adding your city or ZIP code.',
      field: true,
    })

    await userEvent.type(screen.getByLabelText('Home address'), 'Main Street')
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(screen.getByRole('status')).toHaveTextContent(
      'We could not match that address.',
    )
    expect(screen.getByLabelText('Home address')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
    expect(screen.getByLabelText('Home address')).toHaveFocus()
  })

  it('leaves the field alone when the lookup itself failed', async () => {
    renderForm({
      text: 'The district lookup is not responding. Try again in a moment.',
      field: false,
    })

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Find My Maps' }))

    expect(screen.getByRole('status')).toHaveTextContent('is not responding')
    expect(screen.getByLabelText('Home address')).not.toHaveAttribute(
      'aria-invalid',
    )
  })

  it('stops the address at the length the server accepts', () => {
    renderForm()

    expect(screen.getByLabelText('Home address')).toHaveAttribute(
      'maxlength',
      '200',
    )
  })

  // Without this the form stays on "Checking…" for good: the field is readOnly
  // and the button aria-disabled, so only a reload gets the visitor out.
  it('unlocks the form when the lookup throws rather than answering', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('pushState blocked'))

    render(
      <AddressForm
        variant="home"
        submitLabel="Find My Maps"
        onSubmit={onSubmit}
      />,
    )

    await userEvent.type(
      screen.getByLabelText('Home address'),
      '1420 Maple Street{enter}',
    )

    expect(
      await screen.findByRole('button', { name: 'Find My Maps' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Home address')).not.toHaveAttribute('readonly')
    expect(screen.getByRole('status')).toHaveTextContent('Something went wrong.')
  })

  it('unlocks the locate button when that lookup throws too', async () => {
    stubGeolocation((success: (position: unknown) => void) => {
      success({ coords: { latitude: 42.1, longitude: -71.2 } })
    })

    render(
      <AddressForm
        variant="home"
        submitLabel="Find My Maps"
        onSubmit={vi.fn().mockResolvedValue(null)}
        onLocate={vi.fn().mockRejectedValue(new Error('pushState blocked'))}
      />,
    )

    await userEvent.click(
      screen.getByRole('button', { name: /use my current location/i }),
    )

    expect(
      await screen.findByRole('button', { name: /use my current location/i }),
    ).not.toHaveAttribute('aria-disabled')
    expect(screen.getByRole('status')).toHaveTextContent('Something went wrong.')
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
