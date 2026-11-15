import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { CrosshairIcon, PinIcon, SpinnerIcon } from './icons'
import type { Coordinates } from '../routing/routes'
import { COVERED_STATES, type LookupNotice } from '../districts/lookups'
import './AddressForm.css'

// Submitting waits on the caller's lookup, so a place that can't be found is
// said here rather than on a page with nothing to show.

type AddressFormProps = {
  variant: 'home' | 'header'
  submitLabel: string
  initialAddress?: string
  /** Resolves to the message to show, or null once the page has moved on. */
  onSubmit: (address: string) => Promise<LookupNotice | null>
  /** Passing this shows the "Use my current location" button. */
  onLocate?: (coords: Coordinates) => Promise<LookupNotice | null>
}

/** DistrictsController.MaxAddressLength */
const MAX_ADDRESS_LENGTH = 200

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
  const [message, setMessage] = useState<LookupNotice | null>(null)
  const [busy, setBusy] = useState<'checking' | 'locating' | null>(null)

  useEffect(() => {
    mounted.current = true

    return () => {
      mounted.current = false
    }
  }, [])

  /** Shows any problem the caller reports, and always clears busy. */
  async function runCaller(
    call: () => Promise<LookupNotice | null | undefined>,
    focusField: boolean,
  ) {
    try {
      const problem = await call()

      if (mounted.current && problem) {
        setMessage(problem)

        if (focusField) {
          inputRef.current?.focus()
        }
      }
    } catch {
      if (mounted.current) {
        setMessage({ text: 'Something went wrong. Try again.', field: false })
      }
    } finally {
      if (mounted.current) {
        setBusy(null)
      }
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    // The buttons are aria-disabled rather than disabled, so they still fire.
    if (busy) {
      return
    }

    const trimmed = address.trim()

    if (trimmed === '') {
      setMessage({ text: 'Enter an address to continue.', field: true })
      inputRef.current?.focus()
      return
    }

    setMessage(null)
    setBusy('checking')
    await runCaller(() => onSubmit(trimmed), true)
  }

  function handleLocate() {
    if (busy) {
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
    setBusy('locating')

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (mounted.current) {
          void runCaller(
            async () =>
              onLocate?.({
                lat: position.coords.latitude,
                lon: position.coords.longitude,
              }),
            false,
          )
        }
      },
      () => {
        if (mounted.current) {
          setBusy(null)
          setMessage({
            text: 'We could not get your location. Enter an address instead.',
            field: false,
          })
        }
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
            maxLength={MAX_ADDRESS_LENGTH}
            value={address}
            // Not editable while checking: the answer is about the text sent.
            readOnly={busy === 'checking'}
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
        <button
          type="submit"
          className="address-form__submit"
          aria-disabled={busy !== null || undefined}
        >
          {busy === 'checking' ? (
            <>
              <SpinnerIcon className="spin" />
              Checking…
            </>
          ) : (
            submitLabel
          )}
        </button>
      </div>

      {onLocate === undefined ? null : (
        <div className="address-form__secondary">
          <button
            type="button"
            className="address-form__locate"
            onClick={handleLocate}
            aria-disabled={busy !== null || undefined}
          >
            <CrosshairIcon size={15} />
            {busy === 'locating' ? 'Finding you…' : 'Use my current location'}
          </button>
          <span className="address-form__divider" aria-hidden="true" />
          <p className="address-form__note">
            Addresses in {COVERED_STATES}. Used only to look up your
            districts.
          </p>
        </div>
      )}

      {/* Always present, so the live region exists before it has anything to say. */}
      <p id={messageId} className="address-form__message" role="status">
        {message?.text}
      </p>
    </form>
  )
}
