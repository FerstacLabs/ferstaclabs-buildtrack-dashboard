# Deployment security audit and operator actions

## Changes

- Removed the previous public-IP fallback from frontend code/UI/docs. Vercel retains its HTTPS /backend rewrite before SPA routing.
- Removed hardcoded PostgreSQL passwords and default encryption key from compose/appsettings/services. Production validates explicit environment secrets before opening listeners.
- Removed predictable SkySnap seed password; seeding requires explicit credentials. Demo owner seed no longer resets existing hashes. Production destructive demo-reset flags are rejected.
- Image-only production compose, external existing DB volume, explicit shared data bind, registry CI, runtime-only native SDK mount, API loopback bind, private PostgreSQL, 7000/9500 unchanged.
- Serialized all initializer work with PostgreSQL advisory lock, and disabled production worker initialization. No schema rollback, data deletion or password rotation is performed.

## History and rotation checklist

Tracked history previously contained the default database credential, example/default encryption key and predictable SkySnap demo password. Removing them from HEAD does not remove history. Treat any live account using those published defaults as compromised. This change intentionally does not rewrite history or reset production users.

1. Rotate the PostgreSQL role password if it ever used the committed default, with a coordinated DB role + environment update. Merely editing POSTGRES_PASSWORD on an existing volume does not change the role.
2. Rotate any SkySnap owner, supervisor or supply accounts using the documented demo password; use controlled account-specific resets, not destructive demo re-seeding.
3. If the old default BUILDTRACK_SECRET_KEY was used, plan explicit device-secret re-encryption before changing the key. Blind key replacement makes stored camera passwords unreadable. Preserve the current strong production key during migration.
4. Rotate any weak/development JWT signing key if used in production; this invalidates existing tokens and requires sign-in again.
5. If actual Dahua/OpenAI/registry secrets were ever committed in private branches or untracked deployment copies, rotate them with the corresponding provider/device. This audit cannot establish credentials on the VPS from repository contents. No real production secrets were intentionally read or printed.

High-confidence tracked/history searches cover private-key headers, OpenAI/GitHub token patterns and literal Dahua password environment entries. No history rewrite is performed. Pattern scanning is not a proof that no secret exists: operator review of private branches, GitHub secret scanning and provider credential inventories remains necessary.

The targeted scan returned no private-key, OpenAI/GitHub token or literal Dahua password matches in tracked files/history. The known default/demo credentials above were separately confirmed in existing tracked configuration/documentation. The old public-IP / default DB credential scan at HEAD returns no matches.

## Local validation

- `dotnet build backend/BuildTrack.sln`: passed, zero warnings/errors.
- `dotnet test backend/BuildTrack.sln --no-build`: 331 passed, 2 PostgreSQL integration tests skipped locally because no isolated PostgreSQL was available. CI supplies PostgreSQL and runs both, plus API health/DB-disconnect smoke checks.
- `npm run build`: passed (existing bundle-size warning).
- Both source-build and production Compose `config --quiet`: passed with synthetic validation secrets; missing required production values fail interpolation as intended.
- Bash syntax and `node scripts/deployment-audit.mjs`: passed.
- Docker image build attempts could not connect to the local Docker Desktop Linux daemon. Linux image builds run in CI; licensed native SDK load/export preflight runs on VPS. No claim of a local Docker/native hardware test.
- Existing repository lint is not clean: full scan encounters ESLint/parser incompatibility and generated artifacts; focused scan reports existing React effect rules in Devices/Settings. No unrelated UI behavior was rewritten to suppress these findings.

## Verified Linux CI result

GitHub Actions run [36780321903](https://github.com/FerstacLabs/ferstaclabs-buildtrack-dashboard/actions/runs/36780321903) completed successfully for implementation commit `74f422c`:

- 333 tests passed, zero failures, zero skips, including independent PostgreSQL advisory-lock contention/release and concurrent full database initializers.
- API readiness returned success with PostgreSQL available and HTTP 503 after the test database container stopped.
- Frontend build and Release backend build passed.
- Both linux/amd64 production Docker images built and were pushed to GHCR with SHA and main tags.
- The minimal deployment archive was uploaded as `buildtrack-deployment`.

The native proprietary SDK/device/VPS check still must be performed by the deployment preflight and operator; CI does not contain licensed vendor binaries. A follow-up pins runner OS to Ubuntu 24.04 and checks every shell script individually, avoiding future ubuntu-latest migration drift.

## Deliberate boundaries

No firewall rules changed. No live VPS commands run by this change. No native parser, attendance classification, identity policy, SaaS auth/session design or frontend page layout refactored. SDK binaries are not published without vendor redistribution approval. Runtime-only SDK still needs licensed artifacts and a real terminal smoke test. API Swagger behavior remains unchanged; decide separately whether to restrict docs at host Nginx.
