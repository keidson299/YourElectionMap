import { SurveyGround } from './SurveyGround'
import {
  NARROW_FOCUS,
  NARROW_TICKS,
  OVERVIEW,
  WIDE_FOCUS,
  WIDE_TICKS,
} from './surveyMapGeometry'
import './SurveyMap.css'

// The full survey sheet behind the home card. It is decoration, not
// information, so it is hidden from assistive technology entirely.
//
// The two focus groups are both rendered and swapped by a media query rather
// than by measuring the window, because the phone board slices a narrower
// window out of the same drawing and lands on a different district.

export function SurveyMap() {
  return (
    <svg
      viewBox={OVERVIEW.viewBox}
      preserveAspectRatio="xMidYMid slice"
      className="survey-map"
      aria-hidden="true"
      focusable="false"
    >
      <SurveyGround ground={OVERVIEW.ground} standIn />

      <FocusGroup variant="wide" focus={WIDE_FOCUS} />
      <FocusGroup variant="narrow" focus={NARROW_FOCUS} />

      <g className="survey-map__ticks survey-map__ticks--wide">
        {WIDE_TICKS.top.map((tick) => (
          <text key={`top-${tick.value}`} x={tick.x} y={24}>
            {tick.value}
          </text>
        ))}
        {WIDE_TICKS.right.map((tick) => (
          <text key={`right-${tick.value}`} x={1548} y={tick.y}>
            {tick.value}
          </text>
        ))}
      </g>

      <g className="survey-map__ticks survey-map__ticks--narrow">
        {NARROW_TICKS.map((tick) => (
          <text key={tick.value} x={tick.x} y={28}>
            {tick.value}
          </text>
        ))}
      </g>
    </svg>
  )
}

type FocusGroupProps = {
  variant: 'wide' | 'narrow'
  focus: typeof WIDE_FOCUS
}

/** The address's district, hatched, with the pin over the address. */
function FocusGroup({ variant, focus }: FocusGroupProps) {
  const { x, y } = focus.center

  return (
    <g className={`survey-map__focus survey-map__focus--${variant}`}>
      <polygon className="survey-map__selected" points={focus.polygon} />
      <path className="survey-map__hatching" d={focus.hatching} />
      <path className="survey-map__crosshair" d={focus.crosshair} />
      <circle className="survey-map__ring" cx={x} cy={y} r={focus.ringRadius} />
      <circle className="survey-map__pin" cx={x} cy={y} r={focus.pinRadius} />
    </g>
  )
}
