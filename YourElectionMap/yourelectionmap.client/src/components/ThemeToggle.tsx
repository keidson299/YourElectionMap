import {
  DarkThemeIcon,
  LightThemeIcon,
  SystemThemeIcon,
} from './icons'
import { useTheme, type ThemePreference } from '../theme/useTheme'
import './ThemeToggle.css'

const LABELS: Record<ThemePreference, string> = {
  system: 'System',
  light: 'Light',
  dark: 'Dark',
}

const NEXT: Record<ThemePreference, ThemePreference> = {
  system: 'light',
  light: 'dark',
  dark: 'system',
}

const ICONS: Record<ThemePreference, typeof SystemThemeIcon> = {
  system: SystemThemeIcon,
  light: LightThemeIcon,
  dark: DarkThemeIcon,
}

type ThemeToggleProps = {
  className?: string
}

export function ThemeToggle({ className }: ThemeToggleProps) {
  const { preference, cycleTheme } = useTheme()
  const Icon = ICONS[preference]

  return (
    <button
      type="button"
      className={['theme-toggle', className].filter(Boolean).join(' ')}
      onClick={cycleTheme}
      title={`Switch to ${LABELS[NEXT[preference]].toLowerCase()} theme`}
    >
      <Icon size={15} />
      <span>
        Theme<span className="visually-hidden">: currently</span>{' '}
        {LABELS[preference].toLowerCase()}
      </span>
    </button>
  )
}
