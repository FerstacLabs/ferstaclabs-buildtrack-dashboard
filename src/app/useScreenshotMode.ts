import { useEffect } from 'react'

const SCREENSHOT_MODE_CLASS = 'bt-screenshot-mode'

const isScreenshotModeUrl = () => {
  if (typeof window === 'undefined') return false
  const value = new URLSearchParams(window.location.search).get('screenshot')
  return ['1', 'true', 'yes'].includes((value ?? '').toLowerCase())
}

const applyScreenshotModeClass = () => {
  const enabled = isScreenshotModeUrl()
  document.documentElement.classList.toggle(SCREENSHOT_MODE_CLASS, enabled)
  document.body.classList.toggle(SCREENSHOT_MODE_CLASS, enabled)
  document.getElementById('root')?.classList.toggle(SCREENSHOT_MODE_CLASS, enabled)

  if (enabled) {
    window.requestAnimationFrame(() => {
      window.dispatchEvent(new Event('resize'))
    })
  }
}

export const useScreenshotMode = () => {
  useEffect(() => {
    const originalPushState = window.history.pushState
    const originalReplaceState = window.history.replaceState

    const notifyUrlChange = () => {
      applyScreenshotModeClass()
    }

    window.history.pushState = function pushState(...args) {
      const result = originalPushState.apply(this, args)
      notifyUrlChange()
      return result
    }

    window.history.replaceState = function replaceState(...args) {
      const result = originalReplaceState.apply(this, args)
      notifyUrlChange()
      return result
    }

    applyScreenshotModeClass()
    window.addEventListener('popstate', notifyUrlChange)

    return () => {
      window.history.pushState = originalPushState
      window.history.replaceState = originalReplaceState
      window.removeEventListener('popstate', notifyUrlChange)
      document.documentElement.classList.remove(SCREENSHOT_MODE_CLASS)
      document.body.classList.remove(SCREENSHOT_MODE_CLASS)
      document.getElementById('root')?.classList.remove(SCREENSHOT_MODE_CLASS)
    }
  }, [])
}
