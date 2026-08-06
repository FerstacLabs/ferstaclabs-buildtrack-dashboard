'use client'

import { FormEvent } from 'react'
import { contactConfig, content, type Locale } from '@/content/site'

export const ContactForm = ({ locale }: { locale: Locale }) => {
  const t = content[locale]

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const subject = encodeURIComponent(`FerstacLabs inquiry - ${String(data.get('company') || data.get('name') || '')}`)
    const body = encodeURIComponent([
      `${t.contactForm.name}: ${data.get('name') || ''}`,
      `${t.contactForm.company}: ${data.get('company') || ''}`,
      `${t.contactForm.email}: ${data.get('email') || ''}`,
      '',
      `${t.contactForm.message}:`,
      data.get('message') || '',
    ].join('\n'))
    window.location.href = `mailto:${contactConfig.email}?subject=${subject}&body=${body}`
  }

  return (
    <form className="contact-form" onSubmit={handleSubmit}>
      <label>
        <span>{t.contactForm.name}</span>
        <input name="name" required autoComplete="name" />
      </label>
      <label>
        <span>{t.contactForm.company}</span>
        <input name="company" autoComplete="organization" />
      </label>
      <label>
        <span>{t.contactForm.email}</span>
        <input name="email" required type="email" autoComplete="email" />
      </label>
      <label className="full">
        <span>{t.contactForm.message}</span>
        <textarea name="message" required rows={5} />
      </label>
      <button type="submit">{t.contactForm.submit}</button>
    </form>
  )
}
