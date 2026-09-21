import type { MouseEvent, ReactNode } from 'react'
import { navigate } from './history'

// A real anchor, so middle-click and open-in-new-tab behave, that navigates in
// place for an ordinary left click.

type AppLinkProps = {
  href: string
  className?: string
  children: ReactNode
}

export function AppLink({ href, className, children }: AppLinkProps) {
  function handleClick(event: MouseEvent<HTMLAnchorElement>) {
    const modified =
      event.metaKey || event.ctrlKey || event.shiftKey || event.altKey

    if (event.defaultPrevented || event.button !== 0 || modified) {
      return
    }

    event.preventDefault()
    navigate(href)
  }

  return (
    <a href={href} className={className} onClick={handleClick}>
      {children}
    </a>
  )
}
