import { useState } from 'react'
import { SpinnerIcon } from '../icons'
import { SurveyGround } from '../SurveyGround'
import { DETAIL_GROUND, OVERVIEW } from '../surveyMapGeometry'
import { problemCard, type LookupProblem } from '../../districts/lookups'
import { DESIGN, fitFrame, viewBoxOf } from '../../map/frame'
import type { LookupTarget } from '../../routing/routes'
import './LookupStatus.css'

// What the map page shows while it has no result to draw: the survey sheet with
// nothing on it, and one card saying where the lookup got to. Reached by opening
// or reloading a map URL — a lookup started from a form is already in hand by
// the time the page changes.

type LookupStatusProps = {
  state: 'loading' | LookupProblem
  target: LookupTarget
  onRetry: () => void
}

// The middle of the sheet at the spec's design span, so the grid is drawn at the
// weight it has on the result map.
const SHEET_VIEW_BOX = viewBoxOf(
  fitFrame(
    [OVERVIEW.ground.width / 2, OVERVIEW.ground.height / 2],
    [],
    DESIGN.desktop.span,
  ),
)

export function LookupStatus({ state, target, onRetry }: LookupStatusProps) {
  // Set once Try again has been pressed, so the button can stay put while the
  // lookup it started runs rather than unmounting and dropping focus to nowhere.
  const [retried, setRetried] = useState(false)

  const waiting = state === 'loading'
  const place = 'address' in target ? target.address : 'your location'
  const card = waiting
    ? { heading: `Looking up districts for ${place}`, body: '' }
    : problemCard(state, target)

  function handleRetry() {
    // aria-disabled rather than disabled, so the button still fires.
    if (waiting) {
      return
    }

    setRetried(true)
    onRetry()
  }

  return (
    <main className={waiting ? 'lookup-status lookup-status--waiting' : 'lookup-status'}>
      <svg
        className="lookup-status__sheet"
        viewBox={SHEET_VIEW_BOX}
        preserveAspectRatio="xMidYMid meet"
        aria-hidden="true"
        focusable="false"
      >
        <SurveyGround ground={DETAIL_GROUND} />
      </svg>

      <div className="lookup-status__card card">
        {/* Hidden while waiting: the news is the line below, and a heading that
            announced itself first would say nothing yet. Focusable either way,
            because App moves focus here on a page change. */}
        <h1
          className={waiting ? 'visually-hidden' : 'lookup-status__title'}
          tabIndex={-1}
        >
          {card.heading}
        </h1>

        {/* One live region across all three states, so an answer that arrives
            after the page has loaded is announced. */}
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
