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

type SurveyMapProps = {
  className?: string
}

export function SurveyMap({ className }: SurveyMapProps) {
  return (
    <svg
      viewBox={OVERVIEW.viewBox}
      preserveAspectRatio="xMidYMid slice"
      className={['survey-map', className].filter(Boolean).join(' ')}
      aria-hidden="true"
      focusable="false"
    >
      <SurveyGround ground={OVERVIEW.ground} />

      <g className="survey-map__focus survey-map__focus--wide">
        <polygon className="survey-map__selected" points={WIDE_FOCUS.polygon} />
        <path className="survey-map__hatching" d={WIDE_FOCUS.hatching} />
        <path className="survey-map__crosshair" d={WIDE_FOCUS.crosshair} />
        <circle
          className="survey-map__ring"
          cx={WIDE_FOCUS.center.x}
          cy={WIDE_FOCUS.center.y}
          r={WIDE_FOCUS.ringRadius}
        />
        <circle
          className="survey-map__pin"
          cx={WIDE_FOCUS.center.x}
          cy={WIDE_FOCUS.center.y}
          r={WIDE_FOCUS.pinRadius}
        />
      </g>

      <g className="survey-map__focus survey-map__focus--narrow">
        <polygon
          className="survey-map__selected"
          points={NARROW_FOCUS.polygon}
        />
        <path className="survey-map__hatching" d={NARROW_FOCUS.hatching} />
        <path className="survey-map__crosshair" d={NARROW_FOCUS.crosshair} />
        <circle
          className="survey-map__ring"
          cx={NARROW_FOCUS.center.x}
          cy={NARROW_FOCUS.center.y}
          r={NARROW_FOCUS.ringRadius}
        />
        <circle
          className="survey-map__pin"
          cx={NARROW_FOCUS.center.x}
          cy={NARROW_FOCUS.center.y}
          r={NARROW_FOCUS.pinRadius}
        />
      </g>

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
