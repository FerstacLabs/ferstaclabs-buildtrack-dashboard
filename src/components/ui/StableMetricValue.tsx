import { useLayoutEffect, useMemo, useRef } from 'react'

interface StableMetricValueProps {
  value: string | number
  className?: string
  title?: string
}

export const StableMetricValue = ({ className, title, value }: StableMetricValueProps) => {
  const text = useMemo(() => String(value ?? ''), [value])
  const valueRef = useRef<HTMLSpanElement | null>(null)

  useLayoutEffect(() => {
    const syncText = () => {
      if (valueRef.current && valueRef.current.textContent !== text) {
        valueRef.current.textContent = text
      }
    }

    syncText()

    if (typeof window === 'undefined') return undefined

    const frameId = window.requestAnimationFrame(syncText)
    const timeoutIds = [
      window.setTimeout(syncText, 50),
      window.setTimeout(syncText, 250),
    ]
    const observer = typeof MutationObserver !== 'undefined'
      ? new MutationObserver(syncText)
      : undefined

    if (observer && valueRef.current) {
      observer.observe(valueRef.current, {
        characterData: true,
        childList: true,
        subtree: true,
      })
    }

    return () => {
      window.cancelAnimationFrame(frameId)
      timeoutIds.forEach((timeoutId) => window.clearTimeout(timeoutId))
      observer?.disconnect()
    }
  }, [text])

  return (
    <span
      ref={valueRef}
      className={className}
      data-stable-metric-value={text}
      title={title ?? text}
    >
      {text}
    </span>
  )
}
