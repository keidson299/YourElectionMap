import type { ReactNode } from 'react'

// The line icons from design/canvas4/Main.dc.html. All are decorative: they sit
// beside text that already says what they mean, so they are hidden from
// assistive technology and take their colour from the element around them.

type IconProps = {
  size?: number
  className?: string
}

type IconFrameProps = {
  size: number
  className?: string
  /** Stroke width on the 24-unit grid; it differs icon to icon in the spec. */
  weight: number
  roundJoin?: boolean
  children: ReactNode
}

function Icon({
  size,
  className,
  weight,
  roundJoin = false,
  children,
}: IconFrameProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={weight}
      strokeLinecap="round"
      strokeLinejoin={roundJoin ? 'round' : undefined}
      className={className}
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  )
}

export function LayersIcon({ size = 14, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.9} roundJoin>
      <path d="M9 4 3 6.5v14L9 18l6 2.5 6-2.5v-14L15 6.5 9 4Z" />
      <path d="M9 4v14M15 6.5v14" />
    </Icon>
  )
}

export function PinIcon({ size = 18, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.7} roundJoin>
      <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" />
      <circle cx="12" cy="10" r="3" />
    </Icon>
  )
}

export function CrosshairIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.9}>
      <circle cx="12" cy="12" r="7" />
      <circle cx="12" cy="12" r="2.4" fill="currentColor" stroke="none" />
      <path d="M12 2v3M12 19v3M2 12h3M19 12h3" />
    </Icon>
  )
}

export function CalendarIcon({ size = 16, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.7}>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M3 10h18M8 3v4M16 3v4" />
    </Icon>
  )
}

export function PeopleIcon({ size = 16, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.7}>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.9" />
    </Icon>
  )
}

export function BallotIcon({ size = 16, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.7}>
      <path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9Z" />
      <path d="M14 3v6h6M9 14h6" />
    </Icon>
  )
}

export function ChevronIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={2}>
      <path d="M9 5l7 7-7 7" />
    </Icon>
  )
}

export function CheckIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={2.2}>
      <path d="M20 6L9 17l-5-5" />
    </Icon>
  )
}

export function SystemThemeIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.8} roundJoin>
      <rect x="3" y="4" width="18" height="13" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </Icon>
  )
}

export function LightThemeIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.8}>
      <circle cx="12" cy="12" r="4.2" />
      <path d="M12 2.5v2.4M12 19.1v2.4M2.5 12h2.4M19.1 12h2.4M5.3 5.3l1.7 1.7M17 17l1.7 1.7M18.7 5.3 17 7M7 17l-1.7 1.7" />
    </Icon>
  )
}

export function DarkThemeIcon({ size = 15, className }: IconProps) {
  return (
    <Icon size={size} className={className} weight={1.8} roundJoin>
      <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
    </Icon>
  )
}
