import type { MetadataRoute } from 'next'
import { locales, siteConfig } from '@/content/site'
import { localePath } from '@/lib/locale'

export default function sitemap(): MetadataRoute.Sitemap {
  return locales.map((locale) => ({
    url: `${siteConfig.siteUrl}${localePath(locale)}`,
    lastModified: new Date(),
    changeFrequency: 'monthly',
    priority: locale === 'az' ? 1 : 0.8,
  }))
}
