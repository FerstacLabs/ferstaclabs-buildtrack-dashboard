import { siteConfig } from '@/content/site'

export const Logo = () => (
  <div className="brand-mark" aria-label={siteConfig.name}>
    <span className="brand-symbol">F</span>
    <span className="brand-copy">
      <strong>{siteConfig.logoText}</strong>
      <small>{siteConfig.tagline}</small>
    </span>
  </div>
)
