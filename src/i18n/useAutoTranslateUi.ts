import { useEffect } from 'react'
import type { AppLanguage } from './index'
import { translateUiText } from './uiText'

const textNodeOriginals = new WeakMap<Text, string>()
const translatedAttributeNames = ['placeholder', 'title', 'aria-label', 'alt'] as const

const shouldSkipElement = (element: Element | null) => {
  if (!element) return false
  const tagName = element.tagName.toLowerCase()
  return ['script', 'style', 'textarea', 'code', 'pre'].includes(tagName)
    || element.closest('[data-i18n-skip="true"]')
    || element.closest('[contenteditable="true"]')
}

const translateTextNode = (node: Text, language: AppLanguage) => {
  if (shouldSkipElement(node.parentElement)) return
  const original = textNodeOriginals.get(node) ?? node.nodeValue ?? ''
  if (!textNodeOriginals.has(node)) textNodeOriginals.set(node, original)

  const translated = translateUiText(original, language)
  if (node.nodeValue !== translated) node.nodeValue = translated
}

const originalAttributeName = (attributeName: string) => `data-i18n-original-${attributeName}`

const translateElementAttributes = (element: Element, language: AppLanguage) => {
  if (shouldSkipElement(element)) return

  translatedAttributeNames.forEach((attributeName) => {
    const currentValue = element.getAttribute(attributeName)
    if (!currentValue) return

    const originalAttribute = originalAttributeName(attributeName)
    const originalValue = element.getAttribute(originalAttribute) ?? currentValue
    if (!element.hasAttribute(originalAttribute)) element.setAttribute(originalAttribute, originalValue)

    const translated = translateUiText(originalValue, language)
    if (translated !== currentValue) element.setAttribute(attributeName, translated)
  })
}

const scanNode = (node: Node, language: AppLanguage) => {
  if (node.nodeType === Node.TEXT_NODE) {
    translateTextNode(node as Text, language)
    return
  }

  if (node.nodeType !== Node.ELEMENT_NODE) return
  const element = node as Element
  translateElementAttributes(element, language)

  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT)
  let current = walker.nextNode()
  while (current) {
    translateTextNode(current as Text, language)
    current = walker.nextNode()
  }

  element.querySelectorAll('*').forEach((child) => translateElementAttributes(child, language))
}

export const useAutoTranslateUi = (language: AppLanguage) => {
  useEffect(() => {
    if (typeof document === 'undefined') return undefined

    let frame = 0
    const scan = () => {
      window.cancelAnimationFrame(frame)
      frame = window.requestAnimationFrame(() => scanNode(document.body, language))
    }

    scan()
    const observer = new MutationObserver((mutations) => {
      let shouldScan = false
      mutations.forEach((mutation) => {
        if (mutation.type === 'childList' && mutation.addedNodes.length > 0) shouldScan = true
        if (mutation.type === 'characterData' || mutation.type === 'attributes') shouldScan = true
      })
      if (shouldScan) scan()
    })

    observer.observe(document.body, {
      attributes: true,
      attributeFilter: [...translatedAttributeNames],
      characterData: true,
      childList: true,
      subtree: true,
    })

    return () => {
      observer.disconnect()
      window.cancelAnimationFrame(frame)
    }
  }, [language])
}
