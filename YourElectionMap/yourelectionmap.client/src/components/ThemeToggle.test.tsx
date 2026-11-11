import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ThemeToggle } from './ThemeToggle'

function theme() {
  return document.documentElement.getAttribute('data-theme')
}

beforeEach(() => {
  window.localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('ThemeToggle', () => {
  it('leaves the choice to the system until someone asks otherwise', () => {
    render(<ThemeToggle />)

    expect(screen.getByRole('button', { name: /theme/i })).toHaveTextContent(
      /system/i,
    )
    expect(theme()).toBeNull()
  })

  it('cycles through light and dark and back to system', async () => {
    render(<ThemeToggle />)
    const button = screen.getByRole('button', { name: /theme/i })

    await userEvent.click(button)
    expect(theme()).toBe('light')
    expect(window.localStorage.getItem('yem.theme')).toBe('light')

    await userEvent.click(button)
    expect(theme()).toBe('dark')
    expect(window.localStorage.getItem('yem.theme')).toBe('dark')

    await userEvent.click(button)
    expect(theme()).toBeNull()
    expect(window.localStorage.getItem('yem.theme')).toBeNull()
  })

  it('restores a choice made on an earlier visit', () => {
    window.localStorage.setItem('yem.theme', 'dark')

    render(<ThemeToggle />)

    expect(theme()).toBe('dark')
    expect(screen.getByRole('button', { name: /theme/i })).toHaveTextContent(
      /dark/i,
    )
  })

  it('still works when storage is blocked', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage blocked')
    })

    const { unmount } = render(<ThemeToggle />)
    await userEvent.click(screen.getByRole('button', { name: /theme/i }))

    expect(theme()).toBe('light')

    // A new page mounts a new toggle, which must not forget the choice.
    unmount()
    render(<ThemeToggle />)

    expect(theme()).toBe('light')
    expect(screen.getByRole('button', { name: /theme/i })).toHaveTextContent(
      /light/i,
    )
  })
})
