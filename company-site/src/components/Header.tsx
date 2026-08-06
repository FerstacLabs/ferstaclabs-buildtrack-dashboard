'use client'

import { useState } from 'react'
import { contactConfig, content, type Locale } from '@/content/site'
import { Logo } from './Logo'
import { LanguageSwitcher } from './LanguageSwitcher'

export const Header = ({ locale }: { locale: Locale }) => {
  const [open, setOpen] = useState(false)
  const t = content[locale]

  const navItems = [
    { href: '#services', label: t.nav.services },
    { href: '#products', label: t.nav.products },
    { href: '#about', label: t.nav.about },
    { href: '#partner', label: t.nav.partner },
    { href: '#contact', label: t.nav.contact },
  ]

  return (
    <header className="site-header">
      <a href="#top" className="logo-link" aria-label="FerstacLabs home">
        <Logo />
      </a>

      <button className="mobile-menu-button" type="button" onClick={() => setOpen((current) => !current)} aria-expanded={open}>
        <span />
        <span />
        <span />
      </button>

      <nav className={`site-nav ${open ? 'open' : ''}`}>
        {navItems.map((item) => (
          <a key={item.href} href={item.href} onClick={() => setOpen(false)}>{item.label}</a>
        ))}
        <LanguageSwitcher currentLocale={locale} />
        <a className="nav-cta" href={`https://wa.me/${contactConfig.whatsappNumber}`} target="_blank" rel="noreferrer">{t.common.whatsapp}</a>
      </nav>
    </header>
  )
}
