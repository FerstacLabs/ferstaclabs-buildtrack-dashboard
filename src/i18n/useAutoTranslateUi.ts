import { useEffect } from 'react'
import type { AppLanguage } from './index'
import { translateUiText } from './uiText'

interface TextTranslationState {
  lastSource: string
  lastTranslated: string
}

const textNodeTranslations = new WeakMap<Text, TextTranslationState>()
const translatedAttributeNames = ['placeholder', 'title', 'aria-label', 'alt'] as const
const dynamicDomSelectors = [
  '[data-i18n-skip="true"]',
  '[contenteditable="true"]',
  '.ant-select',
  '.ant-select-selector',
  '.ant-select-selection-item',
  '.ant-select-dropdown',
  '.ant-table',
  '.ant-input',
  '.ant-picker',
  '.ant-modal',
  '.ant-drawer',
  '.ant-dropdown',
].join(',')

const shouldSkipElement = (element: Element | null) => {
  if (!element) return false
  const tagName = element.tagName.toLowerCase()
  return ['script', 'style', 'textarea', 'code', 'pre'].includes(tagName)
    || Boolean(element.closest(dynamicDomSelectors))
}

const translateTextNode = (node: Text, language: AppLanguage) => {
  if (shouldSkipElement(node.parentElement)) return
  const currentValue = node.nodeValue ?? ''
  const previous = textNodeTranslations.get(node)
  const source = previous && currentValue === previous.lastTranslated
    ? previous.lastSource
    : currentValue

  const translated = translateUiText(source, language)
  textNodeTranslations.set(node, { lastSource: source, lastTranslated: translated })
  if (currentValue !== translated) node.nodeValue = translated
}

const originalAttributeName = (attributeName: string) => `data-i18n-original-${attributeName}`

const translateElementAttributes = (element: Element, language: AppLanguage) => {
  if (shouldSkipElement(element)) return

  translatedAttributeNames.forEach((attributeName) => {
    const currentValue = element.getAttribute(attributeName)
    if (!currentValue) return

    const originalAttribute = originalAttributeName(attributeName)
    const previousOriginal = element.getAttribute(originalAttribute)
    const previousTranslated = element.getAttribute(`data-i18n-translated-${attributeName}`)
    const originalValue = previousOriginal && currentValue === previousTranslated
      ? previousOriginal
      : currentValue
    if (previousOriginal !== originalValue) element.setAttribute(originalAttribute, originalValue)

    const translated = translateUiText(originalValue, language)
    element.setAttribute(`data-i18n-translated-${attributeName}`, translated)
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
    let observer: MutationObserver | null = null
    const observe = () => {
      observer?.observe(document.body, {
        attributes: true,
        attributeFilter: [...translatedAttributeNames],
        childList: true,
        subtree: true,
      })
    }

    const scan = () => {
      window.cancelAnimationFrame(frame)
      frame = window.requestAnimationFrame(() => {
        observer?.disconnect()
        scanNode(document.body, language)
        observe()
      })
    }

    scan()
    observer = new MutationObserver((mutations) => {
      let shouldScan = false
      mutations.forEach((mutation) => {
        if (mutation.type === 'childList' && mutation.addedNodes.length > 0) shouldScan = true
        if (mutation.type === 'attributes') shouldScan = true
      })
      if (shouldScan) scan()
    })

    observe()

    return () => {
      observer?.disconnect()
      window.cancelAnimationFrame(frame)
    }
  }, [language])
}
