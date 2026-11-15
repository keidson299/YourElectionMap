import { useState } from 'react'
import { SpinnerIcon } from '../icons'
import { SurveyGround } from '../SurveyGround'
import { COVERED_STATES, type LookupProblem } from '../../districts/lookups'
import type { LookupTarget } from '../../routing/routes'
import './LookupStatus.css'

// What the map page shows with no result to draw. Only reached by opening or
// reloading a map URL: a form's lookup is in hand before the page changes.

type LookupStatusProps = {
  state: 'loading' | LookupProblem
  target: LookupTarget
  onRetry: () => void
}

function problemCard(problem: LookupProblem, target: LookupTarget) {
  if (problem === 'error') {
    return {
      heading: 'The district lookup is not responding',
      body: 'Check your connection, then try again.',
    }
  }

  if ('address' in target) {
    return {
      heading: 'We could not place that address',
      body: `The site covers ${COVERED_STATES}. Check the address, or try adding your city or ZIP code, in the search above.`,
    }
  }

  return {
    heading: 'We could not find districts here',
    body: `The site covers ${COVERED_STATES}. Enter an address in the search above.`,
  }
}

export function LookupStatus({ state, target, onRetry }: LookupStatusProps) {
  // Keeps the button mounted while its retry runs, so focus isn't dropped.
  const [retried, setRetried] = useState(false)

  const waiting = state === 'loading'
  const place = 'address' in target ? target.address : 'your location'
  const card = waiting
    ? { heading: `Looking up districts for ${place}`, body: '' }
    : problemCard(state, target)

  function handleRetry() {
    if (waiting) {
      return
    }

    setRetried(true)
    onRetry()
  }

  return (
    <main className={waiting ? 'lookup-status lookup-status--waiting' : 'lookup-status'}>
      {/* The spec's 620×496 frame, with paper three times that so it fills any window. */}
      <svg
        className="lookup-status__sheet"
        viewBox="-310 -248 620 496"
        preserveAspectRatio="xMidYMid meet"
        aria-hidden="true"
        focusable="false"
      >
        <SurveyGround ground={{ x: -930, y: -744, width: 1860, height: 1488 }} />
      </svg>

      <div className="lookup-status__card card">
        {/* Hidden while waiting, since the line below says it, but still
            focusable: App moves focus here on a page change. */}
        <h1
          className={waiting ? 'visually-hidden' : 'lookup-status__title'}
          tabIndex={-1}
        >
          {card.heading}
        </h1>

        {/* One live region across every state, so a late answer is announced. */}
        <p className="lookup-status__body" role="status">
          {waiting ? (
            <>
              <SpinnerIcon size={16} className="lookup-status__spinner spin" />
              Looking up districts…
            </>
          ) : (
            card.body
          )}
        </p>

        {state === 'error' || (waiting && retried) ? (
          <button
            type="button"
            className="lookup-status__retry"
            aria-disabled={waiting || undefined}
            onClick={handleRetry}
          >
            {waiting ? 'Trying again…' : 'Try again'}
          </button>
        ) : null}
      </div>
    </main>
  )
}
