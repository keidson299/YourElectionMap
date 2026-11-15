import { useEffect, useState } from 'react'
import { DarkThemeIcon, LightThemeIcon, SystemThemeIcon } from './icons'
import './ThemeToggle.css'

// 'system' removes data-theme and leaves it to prefers-color-scheme. index.html
// applies the stored choice before first paint, so the attribute is the
// current choice even when storage refused to hold it.

const PREFERENCES = ['system', 'light', 'dark'] as const
type Preference = (typeof PREFERENCES)[number]

const ICONS = {
  system: SystemThemeIcon,
  light: LightThemeIcon,
  dark: DarkThemeIcon,
}

const STORAGE_KEY = 'yem.theme'

function next(preference: Preference): Preference {
  return PREFERENCES[(PREFERENCES.indexOf(preference) + 1) % PREFERENCES.length]
}

export function ThemeToggle({ className }: { className?: string }) {
  const [preference, setPreference] = useState<Preference>(() => {
    const applied = document.documentElement.getAttribute('data-theme')
    return applied === 'light' || applied === 'dark' ? applied : 'system'
  })
  const Icon = ICONS[preference]

  useEffect(() => {
    const root = document.documentElement

    try {
      if (preference === 'system') {
        root.removeAttribute('data-theme')
        window.localStorage.removeItem(STORAGE_KEY)
      } else {
        root.setAttribute('data-theme', preference)
        window.localStorage.setItem(STORAGE_KEY, preference)
      }
    } catch {
      // Blocked storage: the choice still applies for this visit.
    }
  }, [preference])

  return (
    <button
      type="button"
      className={className ? `theme-toggle ${className}` : 'theme-toggle'}
      onClick={() => setPreference(next)}
      title={`Switch to ${next(preference)} theme`}
    >
      <Icon size={15} />
      <span>
        Theme<span className="visually-hidden">: currently</span> {preference}
      </span>
    </button>
  )
}
