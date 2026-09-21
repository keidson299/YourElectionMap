import type { LayerRole } from '../../districts/selection'
import './LineSwatch.css'

// A short sample of how a boundary is drawn on the map: solid for the
// selection, dashed and dotted for the two neighbours, a faint hairline for a
// district the map is not drawing. Line style, not colour alone, tells them
// apart, so the key still reads in greyscale.

type LineSwatchProps = {
  role: LayerRole
  /** Accent on the map's own key; muted for rows that are not selected. */
  tone?: 'accent' | 'muted'
  width: number
}

export function LineSwatch({ role, tone = 'accent', width }: LineSwatchProps) {
  return (
    <svg
      className={`line-swatch line-swatch--${role} line-swatch--${tone}`}
      width={width}
      height="8"
      aria-hidden="true"
      focusable="false"
    >
      <path d={`M1,4 H${width - 1}`} />
    </svg>
  )
}
