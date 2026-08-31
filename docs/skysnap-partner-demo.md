# SkySnap Partner Demo

## Demo Account

- Tenant: `SkySnap Construction Demo`
- Tenant code: `SKYSNAP-DEMO`
- Owner: Tomasz Odrobiński
- Email: `tomasz.odrobinski@skysnap.pl`
- Demo password: `SkySnapDemo!2026`

For production-like deployments, override the password in the VPS `.env`:

```env
SEED_SKYSNAP_DEMO=true
SEED_SKYSNAP_DEMO_RESET=false
SEED_SKYSNAP_DEMO_EMAIL=tomasz.odrobinski@skysnap.pl
SEED_SKYSNAP_DEMO_PASSWORD=replace-with-partner-demo-password
```

Do not put backend passwords or OpenAI keys into Vercel or frontend code.

## Demo Tenant Description

The SkySnap tenant is an isolated English presentation environment for BuildTrack. It is separate from `BAK-DEMO`, `DEMO`, GOLD MMC and all customer tenants. The seed includes:

- 4 project sites in Poland
- 10 field managers/supervisors
- 48 workers across 6 crews
- English estimate/stage/work-item data
- Warehouse stock and material shortfall examples
- Procurement and supply workflow examples
- Daily field reports with review statuses
- Attendance/payroll-ready seed rows
- Dahua Active Register-ready camera placeholders

## Brand Story

- FerstacLabs is the product and technology owner behind BuildTrack.
- 1Muhasib is the business, accounting and implementation partner for rollout.
- SkySnap is the drone progress and site intelligence partner.

The integration story is simple: BuildTrack manages workforce, daily reports, warehouse/procurement, payroll and site progress. SkySnap adds drone-based visual progress capture, condition evidence, material visibility and aerial comparison over time.

## Suggested Demo Flow

1. Dashboard overview
2. Estimate and project progress
3. Field daily report by supervisor
4. Management approval flow
5. Warehouse/material request
6. Procurement/supply flow
7. Attendance and payroll
8. Camera / Active Register
9. SkySnap drone panel
10. AI assistant executive summary

## SkySnap Embed URL

The drone panel route is:

```text
/skysnap-drone
```

Configure the final SkySnap link here:

```env
VITE_SKYSNAP_EMBED_URL=https://partner-skysnap-url.example
VITE_SKYSNAP_EMBED_ENABLED=false
```

Use this as a Vercel Environment Variable for the management frontend. For local development, put the same value in local `.env`.

Production should keep `VITE_SKYSNAP_EMBED_ENABLED=false` until SkySnap confirms iframe embedding support for the BuildTrack origin. In the default mode, the panel shows a clean "Open SkySnap Platform" action instead of rendering a browser-blocked iframe. If embedding is enabled later, the iframe does not receive a BuildTrack JWT/token, and BuildTrack does not append secrets to the URL.

## Presentation Screenshots

Recommended screenshots:

- English dashboard with SkySnap tenant selected
- Estimate table with stages and work items
- Crews page showing Concrete/Rebar/Masonry/Finishing/MEP/Logistics crews
- Warehouse page with partial stock and shortfall
- Procurement page with approved shortfall workflow
- Live attendance and payroll-ready worker hours
- Camera devices page with Active Register placeholders
- SkySnap drone panel fallback or embedded partner app
- AI assistant asking for a management summary
