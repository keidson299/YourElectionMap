import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SurveyMap } from './SurveyMap'

describe('SurveyMap', () => {
  it('is decoration, so assistive technology never sees it', () => {
    const { container } = render(<SurveyMap />)
    const svg = container.querySelector('svg')

    expect(svg).toHaveAttribute('aria-hidden', 'true')
    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.queryByRole('graphics-document')).toBeNull()
  })

  it('draws the whole sheet on the shared survey ground', () => {
    const { container } = render(<SurveyMap />)

    expect(container.querySelector('svg')).toHaveAttribute(
      'viewBox',
      '0 0 1600 1000',
    )
    expect(
      container.querySelectorAll('.survey-ground__mesh polygon'),
    ).toHaveLength(20)
  })

  it('draws both focus groups, for the two board widths', () => {
    const { container } = render(<SurveyMap />)

    expect(
      container.querySelector('.survey-map__focus--wide'),
    ).toBeInTheDocument()
    expect(
      container.querySelector('.survey-map__focus--narrow'),
    ).toBeInTheDocument()
  })
})
