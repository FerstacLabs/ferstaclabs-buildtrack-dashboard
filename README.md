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

Backend CORS is controlled by:

```env
CORS_ALLOWED_ORIGINS=https://ferstaclabs-buildtrack-dashboard.vercel.app
```

If multiple origins are needed, separate them with commas. The docker-compose file keeps the backend API on port `8080`; Vercel should never point to a frontend container on the VPS.

Project progress endpoints are optional for now. If `/api/project-progress/*` does not exist or the API is unavailable, the frontend keeps using persisted local demo data from `localStorage`.

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
