import { defaultLocale, locales, type Locale } from '@/content/site'

export const isLocale = (value: string): value is Locale => locales.includes(value as Locale)

export const normalizeLocale = (value?: string): Locale => {
  if (value && isLocale(value)) return value
  return defaultLocale
}

export const localePath = (locale: Locale) => (locale === defaultLocale ? '/' : `/${locale}`)

export const alternateLanguages = (siteUrl: string) => ({
  az: siteUrl,
  en: `${siteUrl}/en`,
  ru: `${siteUrl}/ru`,
})
