import type { Metadata } from 'next'
import { notFound } from 'next/navigation'
import { HomePage } from '@/components/HomePage'
import { content, locales, siteConfig, type Locale } from '@/content/site'
import { alternateLanguages, isLocale, localePath } from '@/lib/locale'

type LocaleParams = {
  params: Promise<{ locale: string }>
}

const ogLocales: Record<Locale, string> = {
  az: 'az_AZ',
  en: 'en_US',
  ru: 'ru_RU',
}

export const dynamicParams = false

export function generateStaticParams() {
  return locales.map((locale) => ({ locale }))
}

export async function generateMetadata({ params }: LocaleParams): Promise<Metadata> {
  const { locale } = await params
  if (!isLocale(locale)) notFound()

  const t = content[locale]
  const canonical = `${siteConfig.siteUrl}${localePath(locale)}`

  return {
    title: t.meta.title,
    description: t.meta.description,
    keywords: t.meta.keywords,
    alternates: {
      canonical,
      languages: alternateLanguages(siteConfig.siteUrl),
    },
    openGraph: {
      title: t.meta.title,
      description: t.meta.description,
      url: canonical,
      siteName: siteConfig.name,
      locale: ogLocales[locale],
      type: 'website',
      images: [{ url: siteConfig.founderImage, width: 1200, height: 900, alt: `${siteConfig.name} founder portrait` }],
    },
    twitter: {
      card: 'summary_large_image',
      title: t.meta.title,
      description: t.meta.description,
      images: [siteConfig.founderImage],
    },
  }
}

export default async function LocalePage({ params }: LocaleParams) {
  const { locale } = await params
  if (!isLocale(locale)) notFound()

  return <HomePage locale={locale} />
}
