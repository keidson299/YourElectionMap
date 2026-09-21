import { useEffect, useRef, useState } from 'react'

/**
 * An element's rendered size in CSS pixels, kept current as it resizes. Null
 * until the first measurement, and always null where ResizeObserver is
 * missing, which includes jsdom.
 */
export function useElementSize<T extends Element>() {
  const ref = useRef<T>(null)
  const [size, setSize] = useState<{ width: number; height: number } | null>(
    null,
  )

  useEffect(() => {
    const element = ref.current

    if (element === null || typeof ResizeObserver === 'undefined') {
      return
    }

    const observer = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect

      // A hidden or not-yet-laid-out element reports zero; keep the last size.
      if (width > 0 && height > 0) {
        setSize({ width, height })
      }
    })

    observer.observe(element)

    return () => observer.disconnect()
  }, [])

  return [ref, size] as const
}
