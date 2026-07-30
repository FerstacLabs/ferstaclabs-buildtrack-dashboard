import { createContext, createElement, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { az } from './az'
import { en } from './en'
import { ru } from './ru'

export type AppLanguage = 'az' | 'en' | 'ru'

type Dictionary = Record<string, string>

interface I18nContextValue {
  language: AppLanguage
  setLanguage: (language: AppLanguage) => void
  t: (key: string, fallback?: string) => string
}

export const languageStorageKey = 'buildtrack-language'

export const languageOptions: { value: AppLanguage; labelKey: string }[] = [
  { value: 'az', labelKey: 'app.language.az' },
  { value: 'en', labelKey: 'app.language.en' },
  { value: 'ru', labelKey: 'app.language.ru' },
]

const dictionaries: Record<AppLanguage, Dictionary> = { az, en, ru }

const isAppLanguage = (value: string | null): value is AppLanguage => value === 'az' || value === 'en' || value === 'ru'

const getInitialLanguage = (): AppLanguage => {
  if (typeof window === 'undefined') return 'az'
  const saved = window.localStorage.getItem(languageStorageKey)
  return isAppLanguage(saved) ? saved : 'az'
}

const I18nContext = createContext<I18nContextValue | null>(null)

export const I18nProvider = ({ children }: { children: ReactNode }) => {
  const [language, setLanguageState] = useState<AppLanguage>(getInitialLanguage)

  const setLanguage = useCallback((nextLanguage: AppLanguage) => {
    setLanguageState(nextLanguage)
    window.localStorage.setItem(languageStorageKey, nextLanguage)
  }, [])

  const t = useCallback((key: string, fallback?: string) => (
    dictionaries[language][key] ?? dictionaries.az[key] ?? fallback ?? key
  ), [language])

  const value = useMemo(() => ({ language, setLanguage, t }), [language, setLanguage, t])

  return createElement(I18nContext.Provider, { value }, children)
}

export const useI18n = () => {
  const context = useContext(I18nContext)
  if (!context) throw new Error('useI18n must be used inside I18nProvider')
  return context
}
