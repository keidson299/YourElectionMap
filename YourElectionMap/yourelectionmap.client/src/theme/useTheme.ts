import { useCallback, useEffect, useState } from 'react'

// 'system' leaves the choice to prefers-color-scheme by removing the attribute
// the token file keys off; the other two override it. index.html applies the
// stored value before first paint so nothing flashes the wrong scheme.

export const THEME_PREFERENCES = ['system', 'light', 'dark'] as const

export type ThemePreference = (typeof THEME_PREFERENCES)[number]

const STORAGE_KEY = 'yem.theme'

export function readStoredPreference(): ThemePreference {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)

    if (isThemePreference(stored)) {
      return stored
    }
  } catch {
    // Storage can be blocked or throw outright in private windows.
  }

  return 'system'
}

export function useTheme(): {
  preference: ThemePreference
  cycleTheme: () => void
} {
  const [preference, setPreference] =
    useState<ThemePreference>(readStoredPreference)

  useEffect(() => {
    const root = document.documentElement

    if (preference === 'system') {
      root.removeAttribute('data-theme')
    } else {
      root.setAttribute('data-theme', preference)
    }

    try {
      if (preference === 'system') {
        window.localStorage.removeItem(STORAGE_KEY)
      } else {
        window.localStorage.setItem(STORAGE_KEY, preference)
      }
    } catch {
      // A preference we cannot persist still applies for this visit.
    }
  }, [preference])

  const cycleTheme = useCallback(() => {
    setPreference((current) => {
      const next = THEME_PREFERENCES.indexOf(current) + 1
      return THEME_PREFERENCES[next % THEME_PREFERENCES.length]
    })
  }, [])

  return { preference, cycleTheme }
}

function isThemePreference(value: string | null): value is ThemePreference {
  return (
    value !== null && (THEME_PREFERENCES as readonly string[]).includes(value)
  )
}
