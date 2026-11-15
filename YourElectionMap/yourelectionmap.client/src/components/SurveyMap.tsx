import { SurveyGround } from './SurveyGround'
import {
  DISTRICT_POLYGONS,
  NARROW_FOCUS,
  NARROW_TICKS,
  RIVER_PATH,
  ROAD_PATHS,
  WIDE_FOCUS,
  WIDE_TICKS,
} from './surveyMapGeometry'
import './SurveyMap.css'

// The decorative survey sheet behind the home card. Both focus groups are
// rendered and a media query shows one, because the phone's narrower slice of
// the drawing centres on a different district.

export function SurveyMap() {
  return (
    <svg
      viewBox="0 0 1600 1000"
      preserveAspectRatio="xMidYMid slice"
      className="survey-map"
      aria-hidden="true"
      focusable="false"
    >
      <SurveyGround ground={{ x: 0, y: 0, width: 1600, height: 1000 }} />

      <path className="survey-map__water" d={RIVER_PATH} />

      <g className="survey-map__roads">
        {ROAD_PATHS.map((road) => (
          <path key={road} d={road} />
        ))}
      </g>

      <g className="survey-map__mesh">
        {DISTRICT_POLYGONS.map((points) => (
          <polygon key={points} points={points} />
        ))}
      </g>

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

/** The address's district, hatched, with the pin over the address. */
function FocusGroup({
  variant,
  focus,
}: {
  variant: 'wide' | 'narrow'
  focus: typeof WIDE_FOCUS
}) {
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
