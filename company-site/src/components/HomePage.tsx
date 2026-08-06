import Image from 'next/image'
import { contactConfig, content, siteConfig, type Locale } from '@/content/site'
import { ContactForm } from './ContactForm'
import { Header } from './Header'
import { Logo } from './Logo'

const iconLabels = ['01', '02', '03', '04', '05', '06', '07']

export const HomePage = ({ locale }: { locale: Locale }) => {
  const t = content[locale]
  const telLink = `tel:${contactConfig.phone.replace(/\s+/g, '')}`
  const emailLink = `mailto:${contactConfig.email}`
  const whatsappLink = `https://wa.me/${contactConfig.whatsappNumber}`

  const organizationJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: siteConfig.name,
    url: siteConfig.siteUrl,
    email: contactConfig.email,
    telephone: contactConfig.phone,
    founder: {
      '@type': 'Person',
      name: siteConfig.founderName,
      jobTitle: 'Founder / Independent technical partner',
    },
    sameAs: [],
    description: t.meta.description,
  }

  const personJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'Person',
    name: siteConfig.founderName,
    jobTitle: t.founderRole,
    worksFor: {
      '@type': 'Organization',
      name: siteConfig.name,
      url: siteConfig.siteUrl,
    },
  }

  const serviceJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'ProfessionalService',
    name: siteConfig.name,
    url: siteConfig.siteUrl,
    areaServed: 'AZ',
    serviceType: ['Custom software development', 'ERP-oriented software', 'AI automation', 'Data analytics', 'System integration'],
    email: contactConfig.email,
    telephone: contactConfig.phone,
  }

  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationJsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(personJsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(serviceJsonLd) }} />

      <div id="top" className="site-shell">
        <div className="ambient ambient-one" />
        <div className="ambient ambient-two" />
        <Header locale={locale} />

        <main>
          <section className="hero-section">
            <div className="hero-copy">
              <span className="eyebrow">{t.hero.eyebrow}</span>
              <h1>{t.hero.title}</h1>
              <p>{t.hero.subtitle}</p>
              <div className="hero-actions">
                <a className="button primary" href="#contact">{t.common.startProject}</a>
                <a className="button secondary" href="#services">{t.common.viewServices}</a>
                <a className="button ghost" href={whatsappLink} target="_blank" rel="noreferrer">{t.common.whatsapp}</a>
              </div>
              <div className="trust-strip">
                {t.hero.bullets.map((bullet) => <span key={bullet}>{bullet}</span>)}
              </div>
            </div>

            <div className="hero-visual">
              <div className="portrait-card">
                <Image
                  src={siteConfig.founderImage}
                  alt={`${siteConfig.founderName} - FerstacLabs founder`}
                  fill
                  priority
                  sizes="(max-width: 900px) 90vw, 460px"
                  className="portrait-image"
                />
                <div className="portrait-overlay">
                  <strong>{t.hero.founderCardTitle}</strong>
                  <span>{t.hero.founderCardText}</span>
                </div>
              </div>
              <div className="tech-card floating-card">
                <span>BuildTrack</span>
                <strong>{t.buildTrackMini}</strong>
              </div>
            </div>
          </section>

          <section className="stats-grid" aria-label={t.common.safeStatsLabel}>
            {t.stats.map((stat) => (
              <div key={stat.label} className="stat-card">
                <strong>{stat.value}</strong>
                <span>{stat.label}</span>
              </div>
            ))}
          </section>

          <section id="services" className="section-block">
            <div className="section-heading">
              <span className="eyebrow">{t.sectionEyebrows.services}</span>
              <h2>{t.servicesTitle}</h2>
              <p>{t.servicesSubtitle}</p>
            </div>
            <div className="service-grid">
              {t.services.map((service, index) => (
                <article key={service.title} className="premium-card service-card">
                  <span className="card-index">{iconLabels[index]}</span>
                  <h3>{service.title}</h3>
                  <p>{service.text}</p>
                </article>
              ))}
            </div>
          </section>

          <section className="split-section">
            <div className="section-heading compact">
              <span className="eyebrow">{t.sectionEyebrows.why}</span>
              <h2>{t.whyTitle}</h2>
              <p>{t.whySubtitle}</p>
            </div>
            <div className="value-list">
              {t.why.map((item) => (
                <article key={item.title}>
                  <h3>{item.title}</h3>
                  <p>{item.text}</p>
                </article>
              ))}
            </div>
          </section>

          <section id="products" className="section-block">
            <div className="section-heading">
              <span className="eyebrow">{t.sectionEyebrows.products}</span>
              <h2>{t.productsTitle}</h2>
              <p>{t.productsSubtitle}</p>
            </div>
            <div className="product-grid">
              {t.products.map((product) => (
                <article key={product.title} className="premium-card product-card">
                  <span>{product.tag}</span>
                  <h3>{product.title}</h3>
                  <p>{product.text}</p>
                </article>
              ))}
            </div>
          </section>

          <section id="about" className="founder-section">
            <div className="founder-image-wrap">
              <Image
                src={siteConfig.founderImage}
                alt={`${siteConfig.founderName} office portrait`}
                fill
                sizes="(max-width: 900px) 90vw, 520px"
                className="founder-image"
              />
            </div>
            <div className="founder-copy">
              <span className="eyebrow">{t.founderSubtitle}</span>
              <h2>{t.founderTitle}</h2>
              <h3>{siteConfig.founderName}</h3>
              <strong>{t.founderRole}</strong>
              {t.founderBio.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
              <div className="founder-tags">
                <span>Technosec LTD</span>
                <span>Gain Theory</span>
                <span>PyMC</span>
                <span>FerstacLabs</span>
              </div>
            </div>
          </section>

          <section id="partner" className="partner-section">
            <div className="section-heading compact">
              <span className="eyebrow">{t.sectionEyebrows.partner}</span>
              <h2>{t.partnerTitle}</h2>
              <p>{t.partnerSubtitle}</p>
            </div>
            <div className="partner-grid">
              {t.partnerCards.map((card) => (
                <article key={card.title} className="premium-card">
                  <h3>{card.title}</h3>
                  <p>{card.text}</p>
                </article>
              ))}
            </div>
          </section>

          <section className="process-section">
            <div className="section-heading compact">
              <span className="eyebrow">{t.sectionEyebrows.process}</span>
              <h2>{t.processTitle}</h2>
            </div>
            <div className="process-steps">
              {t.processSteps.map((step, index) => (
                <article key={step.title}>
                  <span>{String(index + 1).padStart(2, '0')}</span>
                  <h3>{step.title}</h3>
                  <p>{step.text}</p>
                </article>
              ))}
            </div>
          </section>

          <section className="faq-section">
            <div className="section-heading compact">
              <span className="eyebrow">{t.sectionEyebrows.faq}</span>
              <h2>{t.faqTitle}</h2>
            </div>
            <div className="faq-list">
              {t.faq.map((entry) => (
                <details key={entry.question}>
                  <summary>{entry.question}</summary>
                  <p>{entry.answer}</p>
                </details>
              ))}
            </div>
          </section>

          <section id="contact" className="contact-section">
            <div className="contact-card">
              <div>
                <span className="eyebrow">{t.sectionEyebrows.contact}</span>
                <h2>{t.contactTitle}</h2>
                <p>{t.contactSubtitle}</p>
                <div className="contact-links">
                  <a href={emailLink}>{contactConfig.email}</a>
                  <a href={telLink}>{contactConfig.phone}</a>
                  <a href={whatsappLink} target="_blank" rel="noreferrer">WhatsApp</a>
                </div>
              </div>
              <ContactForm locale={locale} />
            </div>
          </section>
        </main>

        <footer className="site-footer">
          <Logo />
          <p>{t.footerNote}</p>
          <div>
            <a href={emailLink}>{t.common.contactEmail}</a>
            <a href={whatsappLink} target="_blank" rel="noreferrer">{t.common.whatsapp}</a>
          </div>
        </footer>

        <a className="whatsapp-float" href={whatsappLink} target="_blank" rel="noreferrer" aria-label="WhatsApp">
          WA
        </a>
      </div>
    </>
  )
}
