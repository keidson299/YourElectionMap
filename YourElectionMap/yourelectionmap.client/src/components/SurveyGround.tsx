import {
  DISTRICT_POLYGONS,
  RIVER_PATH,
  ROAD_PATHS,
} from './surveyMapGeometry'
import './SurveyGround.css'

// The survey sheet behind Home's card and the result page's status card: paper
// and a graticule, and for Home a river, a few roads and a mesh of district
// outlines.
// It renders inside an <svg> the caller owns. Pattern ids are fixed, which is
// safe because the two pages never draw a sheet at the same time.

type Rect = { x: number; y: number; width: number; height: number }

type SurveyGroundProps = {
  /** How much paper to lay down, in map units. */
  ground: Rect
  /** The hand-drawn river, roads and mesh, which only Home asks for. */
  standIn?: boolean
}

const GRID_FINE_ID = 'yem-grid-fine'
const GRID_BOLD_ID = 'yem-grid-bold'

export function SurveyGround({ ground, standIn = false }: SurveyGroundProps) {
  return (
    <>
      <defs>
        <pattern
          id={GRID_FINE_ID}
          width="25"
          height="25"
          patternUnits="userSpaceOnUse"
        >
          <path className="survey-ground__grid-fine" d="M25,0 V25 M0,25 H25" />
        </pattern>
        <pattern
          id={GRID_BOLD_ID}
          width="100"
          height="100"
          patternUnits="userSpaceOnUse"
        >
          <path
            className="survey-ground__grid-bold"
            d="M100,0 V100 M0,100 H100"
          />
        </pattern>
      </defs>

      <rect {...ground} className="survey-ground__paper" />
      <rect {...ground} fill={`url(#${GRID_FINE_ID})`} />
      <rect {...ground} fill={`url(#${GRID_BOLD_ID})`} />

      {standIn ? (
        <>
          <path className="survey-ground__water" d={RIVER_PATH} />

          <g className="survey-ground__roads">
            {ROAD_PATHS.map((road) => (
              <path key={road} d={road} />
            ))}
          </g>

          <g className="survey-ground__mesh">
            {DISTRICT_POLYGONS.map((points) => (
              <polygon key={points} points={points} />
            ))}
          </g>
        </>
      ) : null}
    </>
  )
}
