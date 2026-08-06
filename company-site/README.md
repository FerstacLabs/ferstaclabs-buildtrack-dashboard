# FerstacLabs Company Website

Premium multilingual corporate website for FerstacLabs, built with Next.js App Router and TypeScript.

## Folder Structure

```text
company-site/
  public/
    favicon.svg
    images/
      founder-portrait.jpeg
  src/
    app/
      [locale]/page.tsx
      globals.css
      layout.tsx
      page.tsx
      robots.ts
      sitemap.ts
    components/
      ContactForm.tsx
      Header.tsx
      HomePage.tsx
      LanguageSwitcher.tsx
      Logo.tsx
    content/
      site.ts
    lib/
      locale.ts
```

## Editable Content

All main website content, translations, metadata, founder bio, services, product cards, FAQ and contact config live in:

```text
src/content/site.ts
```

The default language is Azerbaijani. English and Russian routes are available at `/en` and `/ru`.

## Assets

The founder office portrait is stored at:

```text
public/images/founder-portrait.jpeg
```

The temporary FerstacLabs monogram/wordmark is implemented in:

```text
src/components/Logo.tsx
```

Replace this component later if a final brand logo file becomes available.

## Environment Variables

Create these variables in Vercel and optionally in `.env.local` for local development:

```env
NEXT_PUBLIC_SITE_URL=https://ferstaclabs.com
NEXT_PUBLIC_CONTACT_EMAIL=reymis01@gmail.com
NEXT_PUBLIC_CONTACT_PHONE=+994502462111
NEXT_PUBLIC_WHATSAPP_NUMBER=994502462111
```

## Local Development

```bash
npm install
npm run dev
```

Open `http://localhost:3000`.

## Validation

```bash
npm run build
npm run typecheck
```

There is no lint script configured in this package yet.

## Vercel Deployment

1. Create a new Vercel project from this repository.
2. Set **Root Directory** to `company-site`.
3. Framework preset: **Next.js**.
4. Build command: `npm run build`.
5. Install command: `npm install`.
6. Add the environment variables listed above.
7. Deploy.

This company website is separate from BuildTrack and other product subdomains. Do not point existing product/app subdomains to this Vercel project.

## Domain Setup

1. In the Vercel project, add:
   - `ferstaclabs.com`
   - `www.ferstaclabs.com`
2. Vercel will show the exact DNS records to add at your DNS provider.
3. Typical Vercel setup:
   - Apex `ferstaclabs.com`: A record to `76.76.21.21`
   - `www`: CNAME to `cname.vercel-dns.com`
4. Choose the primary domain in Vercel and enable redirect from the secondary domain.
5. Keep product domains such as BuildTrack on their current deployment/project.
