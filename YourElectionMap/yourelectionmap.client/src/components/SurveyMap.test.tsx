import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SurveyMap } from './SurveyMap'

describe('SurveyMap', () => {
  it('is decoration, so assistive technology never sees it', () => {
    const { container } = render(<SurveyMap variant="overview" />)
    const svg = container.querySelector('svg')

    expect(svg).toHaveAttribute('aria-hidden', 'true')
    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.queryByRole('graphics-document')).toBeNull()
  })

  it('draws both focus groups on the overview, for the two board widths', () => {
    const { container } = render(<SurveyMap variant="overview" />)

    expect(
      container.querySelector('.survey-map__focus--wide'),
    ).toBeInTheDocument()
    expect(
      container.querySelector('.survey-map__focus--narrow'),
    ).toBeInTheDocument()
  })

  it('highlights no district on the detail view, since nothing is looked up yet', () => {
    const { container } = render(<SurveyMap variant="detail" />)

    expect(container.querySelector('.survey-map__selected')).toBeNull()
    expect(container.querySelector('.survey-map__pin')).toBeInTheDocument()
  })
})
