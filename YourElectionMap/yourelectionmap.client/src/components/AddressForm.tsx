import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { CrosshairIcon, PinIcon, SpinnerIcon } from './icons'
import type { Coordinates } from '../routing/routes'
import { COVERED_STATES } from '../districts/lookups'
import './AddressForm.css'

// One form serves the home card and the result header; only the sizes differ.
// It submits through a real <form> so Enter works, and the label is hidden
// rather than absent, so assistive technology still names the field.
//
// Submitting runs the caller's lookup and waits for it, so a place that cannot
// be found is said here rather than on a page that has nothing to show.

type AddressFormProps = {
  variant: 'home' | 'header'
  submitLabel: string
  initialAddress?: string
  /** Resolves to the message to show, or null once the page has moved on. */
  onSubmit: (address: string) => Promise<Message | null>
  /** Passing this shows the "Use my current location" button. */
  onLocate?: (coords: Coordinates) => Promise<Message | null>
}

/** `field` marks a problem with the address itself, which flags the input. */
type Message = { text: string; field: boolean }

/** The server's cap, from DistrictsController.MaxAddressLength. */
const MAX_ADDRESS_LENGTH = 200

/** When the caller itself threw, which leaves nothing more specific to say. */
const CALLER_FAILED: Message = {
  text: 'Something went wrong. Try again.',
  field: false,
}

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
  const [checking, setChecking] = useState(false)

  const busy = checking || locating

  useEffect(() => {
    mounted.current = true

    return () => {
      mounted.current = false
    }
  }, [])

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
    setChecking(true)

    // finally, so a caller that throws cannot leave the form stuck on "Checking…"
    // with nothing the visitor can do but reload.
    try {
      const problem = await onSubmit(trimmed)

      if (mounted.current && problem) {
        setMessage(problem)
        inputRef.current?.focus()
      }
    } catch {
      if (mounted.current) {
        setMessage(CALLER_FAILED)
      }
    } finally {
      if (mounted.current) {
        setChecking(false)
      }
    }
  }

  function handleLocate() {
    // aria-disabled rather than disabled, so focus stays on the button.
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
    setLocating(true)

    navigator.geolocation.getCurrentPosition(
      async (position) => {
        if (!mounted.current) {
          return
        }

        // Still "Finding you…" while the fix is looked up: to the visitor it is
        // one action. Guarded like handleSubmit, and for the same reason.
        try {
          const problem = await onLocate?.({
            lat: position.coords.latitude,
            lon: position.coords.longitude,
          })

          if (mounted.current && problem) {
            setMessage(problem)
          }
        } catch {
          if (mounted.current) {
            setMessage(CALLER_FAILED)
          }
        } finally {
          if (mounted.current) {
            setLocating(false)
          }
        }
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
            // The server rejects anything longer, so stop it at the field rather
            // than sending it and reporting a failure.
            maxLength={MAX_ADDRESS_LENGTH}
            value={address}
            // Readable and selectable while the lookup runs, but not editable:
            // the answer coming back is about the text that was sent.
            readOnly={checking}
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
          aria-disabled={busy || undefined}
        >
          {checking ? (
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
            aria-disabled={busy || undefined}
          >
            <CrosshairIcon size={15} />
            {locating ? 'Finding you…' : 'Use my current location'}
          </button>
          <span className="address-form__divider" aria-hidden="true" />
          <p className="address-form__note">
            Addresses in {COVERED_STATES}. Used only to look up your
            districts.
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
