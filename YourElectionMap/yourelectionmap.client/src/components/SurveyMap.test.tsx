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
})
