# React + TypeScript + Vite

## BuildTrack deployment notes

BuildTrack frontend is deployed on Vercel. Do not run the production frontend inside the VPS Docker stack.

The backend API stays on the VPS as Docker containers and is currently reachable on the VPS at:

```text
http://46.101.182.202:8080
```

For production browser traffic, expose the backend through an HTTPS reverse proxy such as Nginx:

```text
https://api.ferstaclabs.com -> http://127.0.0.1:8080
```

Set this Vercel environment variable for the frontend:

```env
VITE_API_BASE_URL=https://api.ferstaclabs.com
```

The React app reads only `VITE_API_BASE_URL` for the backend API URL. If it is missing, local development falls back to `http://46.101.182.202:8080`. On Vercel, always set the HTTPS API URL to avoid mixed-content blocking.

BuildTrack uses host-based routing:

```text
buildtrack.ferstaclabs.com       marketing site
app.buildtrack.ferstaclabs.com   management dashboard
field.buildtrack.ferstaclabs.com field / prorab portal
supply.buildtrack.ferstaclabs.com supply / procurement portal
```

For the field portal deployment, set:

```env
VITE_API_BASE_URL=https://api.ferstaclabs.com
VITE_APP_BASE_URL=https://app.buildtrack.ferstaclabs.com
VITE_FIELD_BASE_URL=https://field.buildtrack.ferstaclabs.com
VITE_SUPPLY_BASE_URL=https://supply.buildtrack.ferstaclabs.com
```

Backend CORS is controlled by:

```env
CORS_ALLOWED_ORIGINS=https://app.buildtrack.ferstaclabs.com,https://field.buildtrack.ferstaclabs.com,https://supply.buildtrack.ferstaclabs.com,https://buildtrack.ferstaclabs.com
```

If multiple origins are needed, separate them with commas. The docker-compose file keeps the backend API on port `8080`; Vercel should never point to a frontend container on the VPS.

Project progress data is server-authoritative. Authenticated users load and save the workspace through `/api/project-progress/workspace`; browser storage is limited to harmless UI/session state and legacy one-time migration detection.

## SaaS auth, tenant and license setup

BuildTrack now runs as a tenant-isolated SaaS app:

- `buildtrack.ferstaclabs.com` shows the public marketing landing page.
- `app.buildtrack.ferstaclabs.com` shows the protected application.
- Unauthenticated users are redirected to `/login`.
- Authenticated tenants without an active license are redirected to `/license`.
- Existing demo data is backfilled to the `FerstacLabs Demo` tenant with code `DEMO`.
- Newly registered tenants start with an empty workspace.

Set these backend variables in the VPS `.env` file before recreating the API container:

```env
JWT_SECRET=replace-with-a-long-random-secret
JWT_ISSUER=BuildTrack
JWT_AUDIENCE=BuildTrack.App
JWT_EXPIRES_MINUTES=720

SEED_ADMIN_EMAIL=admin@example.com
SEED_ADMIN_PASSWORD=replace-with-secure-password
SEED_ADMIN_RESET_PASSWORD=false
SEED_ADMIN_FULL_NAME=FerstacLabs Admin
SEED_ADMIN_TENANT_NAME=FerstacLabs Demo

SEED_SUPERVISOR_EMAIL=prorab@example.com
SEED_SUPERVISOR_PASSWORD=replace-with-secure-password
SEED_SUPERVISOR_FULL_NAME=Demo Prorab
SEED_SUPERVISOR_PHONE=+994...
SEED_SUPERVISOR_SITE_ID=

# Optional isolated BAKİNİTY demo tenant. Password values must come from VPS env.
SEED_BAKINITY_DEMO=false
SEED_BAKINITY_DEMO_RESET=false
SEED_BAKINITY_DEMO_EMAIL=eldar@bakinity.az
SEED_BAKINITY_DEMO_PASSWORD=
SEED_BAKINITY_DEMO_PRORAB_PASSWORD=
SEED_BAKINITY_DEMO_SUPPLY_PASSWORD=

CORS_ALLOWED_ORIGINS=https://app.buildtrack.ferstaclabs.com,https://field.buildtrack.ferstaclabs.com,https://supply.buildtrack.ferstaclabs.com,https://buildtrack.ferstaclabs.com
```

If `SEED_ADMIN_PASSWORD` is missing, the initializer does not create an insecure default admin password. The demo tenant still receives an active Unlimited license.

To rotate the seeded admin password without deleting tenant or demo data, set `SEED_ADMIN_RESET_PASSWORD=true` together with the new `SEED_ADMIN_PASSWORD`, restart the API once, then set `SEED_ADMIN_RESET_PASSWORD=false` again.

The seeded demo admin can manage tenant licenses from the app at `/admin/licenses`. The page lists all tenants, generates one-time raw license keys, and can directly activate a selected tenant license for onboarding/demo use.

If `SEED_SUPERVISOR_EMAIL` and `SEED_SUPERVISOR_PASSWORD` are set, the initializer creates or updates a Supervisor account for the demo tenant and assigns it to `SEED_SUPERVISOR_SITE_ID` when provided. Field users inherit the tenant license and cannot activate licenses themselves.

### Browser session isolation

BuildTrack stores the frontend JWT access token in `sessionStorage`, not `localStorage`. This is intentional: several company accounts can stay open on the same origin in separate browser tabs/windows without one login overwriting another tab's API token. Refresh keeps the current tab session, while closing that tab removes its token.

During the migration away from the old shared `localStorage["buildtrack.authToken"]` token, the frontend deletes the legacy value and does not copy it into `sessionStorage`; users may need to log in once after deployment. API requests read the token only through the current tab session.

A normal single HttpOnly cookie is not used for this SPA requirement yet because cookies are shared by all tabs on the same origin and would again make all tabs represent the same active account unless a more complex per-tab session architecture is introduced. Keep HTTPS enabled, use short JWT lifetimes appropriate for the deployment, and maintain strict CSP/XSS hygiene; `sessionStorage` is tab-isolated but JavaScript-readable.

### BAKİNİTY server-authoritative demo

Set `SEED_BAKINITY_DEMO=true` and provide `SEED_BAKINITY_DEMO_PASSWORD` to create the isolated tenant `BAK-DEMO` (`BAKİNİTY MMC`) with owner user `eldar@bakinity.az`. The initializer seeds BAKİNİTY sites, 10 prorab users, procurement users, workers, field smeta items, warehouse stock, warehouse request states, suppliers, one procurement task and a server-side project progress workspace. Seed passwords are never logged or committed.

The BAKİNİTY demo is isolated from existing tenants such as GOLD MMC. To reset only this demo environment, set `SEED_BAKINITY_DEMO_RESET=true`, restart the API/worker once, verify the seed, then set it back to `false`. The reset removes/reseeds only tenant code `BAK-DEMO`; it does not touch other tenant codes.

Project progress data is server-authoritative through PostgreSQL-backed project, stage, work-item, crew and material endpoints. `/api/project-progress/workspace` remains a read/legacy compatibility projection and the visible one-time "Serverə köçür" migration path for old browser snapshots; normal Smeta/crew/material edits use granular backend writes and localStorage keeps only UI/session state.

You can also create a tenant license as the demo/admin account with curl:

```bash
curl -X POST https://api.ferstaclabs.com/api/admin/licenses \
  -H "Authorization: Bearer <admin-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"<tenant-id>","plan":"Business","expiresAt":null,"maxProjects":10,"maxUsers":50,"maxCameras":5}'
```

The response includes the raw `licenseKey` only once. Store only the hash in the database. The tenant activates it from `/license` or with:

```bash
curl -X POST https://api.ferstaclabs.com/api/licenses/activate \
  -H "Authorization: Bearer <tenant-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"BT-..."}'
```

Frontend environment for Vercel:

```env
VITE_API_BASE_URL=/backend
VITE_APP_BASE_URL=https://app.buildtrack.ferstaclabs.com
VITE_MARKETING_BASE_URL=https://buildtrack.ferstaclabs.com
VITE_FIELD_BASE_URL=https://field.buildtrack.ferstaclabs.com
VITE_SUPPLY_BASE_URL=https://supply.buildtrack.ferstaclabs.com
```

Keep the `/backend` rewrite before the SPA fallback in `vercel.json`. The VPS backend still runs in Docker on port `8080`; HTTPS should be provided by Nginx or another reverse proxy.

## Supply / procurement workflow

BuildTrack Supply runs from `supply.buildtrack.ferstaclabs.com` and uses the same backend, tenant, license and PostgreSQL database as the management and field portals. Procurement agents are created by management users and can only access assigned buying tasks from the Supply Portal.

Relevant deployment values:

```env
VITE_SUPPLY_BASE_URL=https://supply.buildtrack.ferstaclabs.com
SUPPLY_ATTACHMENT_STORAGE_PATH=/app/data/supply-attachments
CORS_ALLOWED_ORIGINS=https://app.buildtrack.ferstaclabs.com,https://field.buildtrack.ferstaclabs.com,https://supply.buildtrack.ferstaclabs.com,https://buildtrack.ferstaclabs.com
```

The Docker stack mounts `./data:/app/data`, so procurement product photos, receipts and invoices saved under `SUPPLY_ATTACHMENT_STORAGE_PATH` survive container recreation. The warehouse request chain is continuous: field cart request, private availability check, reservation, shortfall procurement need, supply task, evidence upload, management verification, goods receipt, reservation fulfillment and final warehouse issue.

## AI assistant backend setup

The BuildTrack AI assistant must call OpenAI only through the VPS backend. Do not add `OPENAI_API_KEY` or any secret to Vercel or React/Vite environment variables.

Create or update the `.env` file next to `docker-compose.yml` on the VPS:

```env
OPENAI_API_KEY=sk-...
OPENAI_MODEL=gpt-4o-mini
OPENAI_ASSISTANT_ENABLED=true
OPENAI_TTS_ENABLED=true
OPENAI_TTS_MODEL=gpt-4o-mini-tts
OPENAI_TTS_VOICE=shimmer
OPENAI_TTS_FORMAT=mp3
```

## Dahua camera identity mode

The default backend identity behavior is conservative: `DAHUA_IDENTITY_RESOLUTION_MODE=strict_userid`. In this mode Smart Event attendance is accepted only when Dahua `UserID` maps to one active worker and the received `CardName` matches that worker.

For the tested Dahua terminal, Smart Event can send the same `UserID` for different enrolled people. On the VPS demo/prototype deployment use:

```env
DAHUA_IDENTITY_RESOLUTION_MODE=cardname_primary
DAHUA_AUTO_PROVISION_CAMERA_WORKERS=true
DAHUA_MIN_CARDNAME_LENGTH_FOR_AUTOPROVISION=3
DAHUA_CARDNAME_AUTOPROVISION_ALLOWLIST=ilham,tahira
DAHUA_CARDNAME_AUTOPROVISION_DENYLIST=Bx,fj,p1x,J4myH,uiryH
```

`cardname_primary` resolves workers by trusted, high-confidence Smart Event `CardName` first. If enabled, BuildTrack auto-creates a worker for valid camera names such as `ilham` or `tahira` using normal system worker codes like `W-0001`, while suspicious binary candidates such as `Bx`, `fj`, `p1x`, `J4myH`, or `uiryH` remain security-review events.

Then recreate the backend API container:

```bash
docker compose up -d --build buildtrack-api
```

The frontend continues to use only:

```env
VITE_API_BASE_URL=https://api.ferstaclabs.com
```

If the backend has no OpenAI key or the AI endpoint is unavailable, the assistant falls back to the local BuildTrack analysis engine and marks the answer with a compact `Lokal analiz` pill.

Read-aloud audio is generated through the backend endpoint `POST /api/ai/tts`, so the OpenAI key remains server-only. On Vercel, the frontend calls `/backend/api/ai/tts` through the rewrite configured by `VITE_API_BASE_URL=/backend`. If backend TTS is unavailable, the browser speech engine is used as a fallback.

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...

      // Remove tseslint.configs.recommended and replace with this
      tseslint.configs.recommendedTypeChecked,
      // Alternatively, use this for stricter rules
      tseslint.configs.strictTypeChecked,
      // Optionally, add this for stylistic rules
      tseslint.configs.stylisticTypeChecked,

      // Other configs...
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])

```
