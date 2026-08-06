'use client'

import { locales, type Locale } from '@/content/site'
import { localePath } from '@/lib/locale'

const labels: Record<Locale, string> = {
  az: 'AZ',
  en: 'EN',
  ru: 'RU',
}

export const LanguageSwitcher = ({ currentLocale }: { currentLocale: Locale }) => {
  const rememberLanguage = (locale: Locale) => {
    window.localStorage.setItem('ferstaclabs-locale', locale)
  }

  return (
    <div className="language-switcher" aria-label="Language switcher">
      {locales.map((locale) => (
        <a
          key={locale}
          href={localePath(locale)}
          className={locale === currentLocale ? 'active' : ''}
          onClick={() => rememberLanguage(locale)}
        >
          {labels[locale]}
        </a>
      ))}
    </div>
  )
}
