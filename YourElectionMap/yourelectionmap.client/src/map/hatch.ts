// The diagonal hatch over the selected district, drawn as one tile MapLibre
// repeats. The spec rotates its lines −32.5°; a repeating tile needs a slope
// that lands back on a corner, and 2:3 (33.7°) is the nearest that does in a
// small tile. Lines run up to the right, as the spec's do.

/** Perpendicular gap between lines in a 3a×2a tile: 6a/√13. */
const GAP_PER_A = 6 / Math.sqrt(13)

/** The spec's hatch lines are 0.9 of a survey unit at 1.68px a unit. */
const LINE_WIDTH_PER_SPACING = 1.5 / 57
const LINE_OPACITY = 0.45

export type HatchImage = { image: ImageData; pixelRatio: number }

/**
 * A tile of hatch lines `spacing` CSS pixels apart in `color`, at the screen's
 * pixel ratio. Null where there is no canvas to draw on.
 */
export function drawHatch(spacing: number, color: string): HatchImage | null {
  const pixelRatio = Math.max(1, Math.round(window.devicePixelRatio || 1))
  const a = Math.max(4, Math.round(spacing / GAP_PER_A))
  const width = 3 * a * pixelRatio
  const height = 2 * a * pixelRatio

  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height

  let context: CanvasRenderingContext2D | null = null

  try {
    context = canvas.getContext('2d')
  } catch {
    // jsdom has no canvas, and says so by throwing.
  }

  if (context === null) {
    return null
  }

  context.strokeStyle = color
  context.globalAlpha = LINE_OPACITY
  context.lineWidth = Math.max(0.8, spacing * LINE_WIDTH_PER_SPACING) * pixelRatio

  // The tile's diagonal, and the two lines through its opposite corners, so the
  // stroke is whole where four tiles meet.
  context.beginPath()
  for (const offset of [-1, 0, 1]) {
    context.moveTo(offset * width, height)
    context.lineTo((offset + 1) * width, 0)
  }
  context.stroke()

  return { image: context.getImageData(0, 0, width, height), pixelRatio }
}
