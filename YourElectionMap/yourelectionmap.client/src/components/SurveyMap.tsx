import {
  DETAIL_FOCUS,
  DISTRICT_POLYGONS,
  NARROW_FOCUS,
  NARROW_TICKS,
  RIVER_PATH,
  ROAD_PATHS,
  VIEW_BOXES,
  WIDE_FOCUS,
  WIDE_TICKS,
} from './surveyMapGeometry'
import './SurveyMap.css'

// The survey sheet the whole design sits on: a graticule, a river, a few roads
// and the district boundaries. It is decoration, not information, so it is
// hidden from assistive technology entirely.
//
// The two focus groups are both rendered and swapped by a media query rather
// than by measuring the window, because the phone board slices a narrower
// window out of the same drawing and lands on a different district.

type SurveyMapProps = {
  variant: 'overview' | 'detail'
  className?: string
}

const GRID_FINE_ID = 'yem-grid-fine'
const GRID_BOLD_ID = 'yem-grid-bold'

export function SurveyMap({ variant, className }: SurveyMapProps) {
  const { viewBox, ground } = VIEW_BOXES[variant]

  return (
    <svg
      viewBox={viewBox}
      preserveAspectRatio="xMidYMid slice"
      className={['survey-map', `survey-map--${variant}`, className]
        .filter(Boolean)
        .join(' ')}
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <pattern
          id={GRID_FINE_ID}
          width="25"
          height="25"
          patternUnits="userSpaceOnUse"
        >
          <path className="survey-map__grid-fine" d="M25,0 V25 M0,25 H25" />
        </pattern>
        <pattern
          id={GRID_BOLD_ID}
          width="100"
          height="100"
          patternUnits="userSpaceOnUse"
        >
          <path className="survey-map__grid-bold" d="M100,0 V100 M0,100 H100" />
        </pattern>
      </defs>

      <rect {...ground} className="survey-map__ground" />
      <rect {...ground} fill={`url(#${GRID_FINE_ID})`} />
      <rect {...ground} fill={`url(#${GRID_BOLD_ID})`} />

      <path className="survey-map__water" d={RIVER_PATH} />

      <g className="survey-map__roads">
        {ROAD_PATHS.map((road) => (
          <path key={road} d={road} />
        ))}
      </g>

      <g className="survey-map__districts">
        {DISTRICT_POLYGONS.map((points) => (
          <polygon key={points} points={points} />
        ))}
      </g>

      {variant === 'overview' ? (
        <>
          <g className="survey-map__focus survey-map__focus--wide">
            <polygon
              className="survey-map__selected"
              points={WIDE_FOCUS.polygon}
            />
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
            <path
              className="survey-map__crosshair"
              d={NARROW_FOCUS.crosshair}
            />
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
        </>
      ) : (
        // No district is drawn as selected here: nothing has been looked up
        // yet, and a highlighted shape would read as an answer. Slice 2 adds
        // the selected boundary and its two overlapping neighbours.
        <g className="survey-map__focus">
          <path className="survey-map__crosshair" d={DETAIL_FOCUS.crosshair} />
          <circle
            className="survey-map__ring"
            cx={DETAIL_FOCUS.center.x}
            cy={DETAIL_FOCUS.center.y}
            r={DETAIL_FOCUS.ringRadius}
          />
          <circle
            className="survey-map__pin survey-map__pin--outlined"
            cx={DETAIL_FOCUS.center.x}
            cy={DETAIL_FOCUS.center.y}
            r={DETAIL_FOCUS.pinRadius}
          />
        </g>
      )}
    </svg>
  )
}
