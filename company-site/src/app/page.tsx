import type { Metadata } from 'next'
import { content, defaultLocale, siteConfig } from '@/content/site'
import { alternateLanguages } from '@/lib/locale'
import { HomePage } from '@/components/HomePage'

export const metadata: Metadata = {
  title: content.az.meta.title,
  description: content.az.meta.description,
  keywords: content.az.meta.keywords,
  alternates: {
    canonical: siteConfig.siteUrl,
    languages: alternateLanguages(siteConfig.siteUrl),
  },
  openGraph: {
    title: content.az.meta.title,
    description: content.az.meta.description,
    url: siteConfig.siteUrl,
    siteName: siteConfig.name,
    locale: 'az_AZ',
    type: 'website',
    images: [{ url: siteConfig.founderImage, width: 1200, height: 900, alt: `${siteConfig.name} founder portrait` }],
  },
  twitter: {
    card: 'summary_large_image',
    title: content.az.meta.title,
    description: content.az.meta.description,
    images: [siteConfig.founderImage],
  },
}

export default function Page() {
  return <HomePage locale={defaultLocale} />
}
