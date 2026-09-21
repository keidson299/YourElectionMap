import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { CrosshairIcon, PinIcon } from './icons'
import type { Coordinates } from '../routing/routes'
import './AddressForm.css'

// One form serves the home card and the result header; only the sizes differ.
// It submits through a real <form> so Enter works, and the label is hidden
// rather than absent, both decisions recorded in design/canvas3/Notes.dc.html.

type AddressFormProps = {
  variant: 'home' | 'header'
  submitLabel: string
  initialAddress?: string
  onSubmit: (address: string) => void
  /** Passing this shows the "Use my current location" button. */
  onLocate?: (coords: Coordinates) => void
}

/** `field` marks a problem with the address itself, which flags the input. */
type Message = { text: string; field: boolean }

export function AddressForm({
  variant,
  submitLabel,
  initialAddress = '',
  onSubmit,
  onLocate,
}: AddressFormProps) {
  const fieldId = useId()
  const messageId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  // A location fix can arrive after a submitted address has left the page.
  const mounted = useRef(false)

  const [address, setAddress] = useState(initialAddress)
  const [message, setMessage] = useState<Message | null>(null)
  const [locating, setLocating] = useState(false)

  useEffect(() => {
    mounted.current = true

    return () => {
      mounted.current = false
    }
  }, [])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const trimmed = address.trim()

    if (trimmed === '') {
      setMessage({ text: 'Enter an address to continue.', field: true })
      inputRef.current?.focus()
      return
    }

    setMessage(null)
    onSubmit(trimmed)
  }

  function handleLocate() {
    // aria-disabled rather than disabled, so focus stays on the button.
    if (locating) {
      return
    }

    if (!('geolocation' in navigator)) {
      setMessage({
        text: 'This browser cannot share your location. Enter an address instead.',
        field: false,
      })
      return
    }

    setMessage(null)
    setLocating(true)

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (!mounted.current) {
          return
        }

        setLocating(false)
        onLocate?.({
          lat: position.coords.latitude,
          lon: position.coords.longitude,
        })
      },
      () => {
        if (!mounted.current) {
          return
        }

        setLocating(false)
        setMessage({
          text: 'We could not get your location. Enter an address instead.',
          field: false,
        })
      },
      { timeout: 10000 },
    )
  }

  return (
    <form
      className={`address-form address-form--${variant}`}
      noValidate
      onSubmit={handleSubmit}
    >
      <label className="visually-hidden" htmlFor={fieldId}>
        Home address
      </label>

      <div className="address-form__controls">
        <div className="address-form__field">
          <PinIcon
            size={variant === 'home' ? 18 : 16}
            className="address-form__field-icon"
          />
          <input
            id={fieldId}
            ref={inputRef}
            type="text"
            className="address-form__input"
            placeholder="Enter your home address"
            autoComplete="street-address"
            value={address}
            onChange={(event) => {
              setAddress(event.target.value)

              if (message?.field) {
                setMessage(null)
              }
            }}
            aria-invalid={message?.field ? true : undefined}
            aria-describedby={messageId}
          />
        </div>
        <button type="submit" className="address-form__submit">
          {submitLabel}
        </button>
      </div>

      {onLocate === undefined ? null : (
        <div className="address-form__secondary">
          <button
            type="button"
            className="address-form__locate"
            onClick={handleLocate}
            aria-disabled={locating || undefined}
          >
            <CrosshairIcon size={15} />
            {locating ? 'Finding you…' : 'Use my current location'}
          </button>
          <span className="address-form__divider" aria-hidden="true" />
          <p className="address-form__note">
            Address used only to look up your districts.
          </p>
        </div>
      )}

      {/* Always present so the live region exists before it has anything to say. */}
      <p id={messageId} className="address-form__message" role="status">
        {message?.text}
      </p>
    </form>
  )
}
