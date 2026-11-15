import './SurveyGround.css'

// Paper and a graticule, inside an <svg> the caller owns: behind Home's map and
// the result page's status card. The fixed pattern ids are safe because the
// two never draw at the same time.

type Rect = { x: number; y: number; width: number; height: number }

export function SurveyGround({ ground }: { ground: Rect }) {
  return (
    <>
      <defs>
        <pattern id="yem-grid-fine" width="25" height="25" patternUnits="userSpaceOnUse">
          <path className="survey-ground__grid-fine" d="M25,0 V25 M0,25 H25" />
        </pattern>
        <pattern id="yem-grid-bold" width="100" height="100" patternUnits="userSpaceOnUse">
          <path className="survey-ground__grid-bold" d="M100,0 V100 M0,100 H100" />
        </pattern>
      </defs>

      <rect {...ground} className="survey-ground__paper" />
      <rect {...ground} fill="url(#yem-grid-fine)" />
      <rect {...ground} fill="url(#yem-grid-bold)" />
    </>
  )
}
