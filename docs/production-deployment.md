# Production deployment: Vercel + GHCR + VPS

Frontend stays on Vercel. Set `VITE_API_BASE_URL=https://api.buildtrack.ferstaclabs.com` on each production frontend. Without this value only Development uses localhost; production uses `/backend` and the existing Vercel HTTPS rewrite before the SPA fallback. There is no raw public-IP fallback and no frontend Docker container. Marketing, app, field and supply host routing is unchanged.

```
GitHub main -> tests -> linux/amd64 multi-stage Docker builds -> GHCR
Vercel HTTPS frontend -> host Nginx -> 127.0.0.1:8080 API -> private PostgreSQL
Dahua terminal -> TCP 7000/9500 worker -> PostgreSQL + shared /app/data
```

## Images and CI

Workflow: **Production images**, `.github/workflows/production-images.yml`.

- `ghcr.io/ferstaclabs/ferstaclabs-buildtrack-dashboard/buildtrack-api`
- `ghcr.io/ferstaclabs/ferstaclabs-buildtrack-dashboard/buildtrack-dahua-worker`

Tags: `sha-<full commit SHA>` and moving `main`. Pin SHA in production; both images must use the same SHA. Keep old SHA images in GHCR for rollback. Restrict package visibility to private as appropriate in GitHub package settings. The workflow uses only `GITHUB_TOKEN` (`contents:read`, image job `packages:write`), builds PRs without publishing, and uploads **buildtrack-deployment** containing `buildtrack-deployment.tar.gz`. No secrets or SDK are in this archive.

Prerequisites: Ubuntu 24.04 amd64, Docker Engine with Compose v2 supporting `up --wait`, Python3, util-linux/flock, Nginx with existing TLS. No Node, .NET SDK, Git clone, or source checkout on VPS. For a private registry, use a read-only packages token with `docker login ghcr.io --username YOUR_GITHUB_USER --password-stdin`; provide it from a secure prompt, never put it in scripts/history. Configure GitHub package repository access so the workflow can publish.

## Minimal package

```
/opt/ferstaclabs-buildtrack/
  docker-compose.prod.yml
  .env                     # mode 600, not in Git/archive
  deploy.sh
  scripts/deploy.sh
  scripts/backup-db.sh
  docs/
  deployment-history/      # image IDs only, generated
```

Persistent data may remain at its CURRENT absolute path. `/app/data` holds security/attendance smart snapshots and supply attachments; both API and worker mount the same directory. Native runtime is a separate minimal directory, e.g. `/opt/buildtrack-runtime/linux-x64`, not a repo checkout.

### Dahua runtime decision

The SDK is proprietary, ignored in Git, and no redistribution permission is present in the repository. We deliberately do NOT upload it to GHCR. Supply only the licensed **linux-x64 runtime** files from the currently working installation: `libdhnetsdk.so`, `libavnetsdk.so`, `libdhconfigsdk.so`, `libStreamConvertor.so`, `libcrypto.so`, `libssl.so`, plus any additional runtime dependencies required by your exact SDK version. Do not copy headers, demos, full-sdk or source. Use the same verified SDK version, not an untested replacement.

The deployment preflight loads the native library and verifies required symbols using `--sdk-check` inside the actual worker image. This catches absent dependencies/architecture mismatches before stopping services. It does not prove device login; after deploy verify device reconnect, `CLIENT_RealLoadPictureEx` and a known/unknown face. Existing header-probe startup warning may appear because runtime-only packages omit source headers; compiled decoders are unchanged and are not gated by the header probe. Linux native behavior still requires the VPS smoke test.

## Required configuration

Copy `.env.example` only for NEW installations. For migration copy the existing `.env` and preserve all secret values.

- `BUILDTRACK_DB_PASSWORD`: existing database-role password. Changing env does NOT alter a PostgreSQL role password.
- `JWT_SECRET`, `BUILDTRACK_SECRET_KEY`: explicit non-default >=32 characters; preserve the existing encryption key or stored device passwords become unreadable.
- `BUILDTRACK_IMAGE_REGISTRY`, `BUILDTRACK_IMAGE_TAG`, `COMPOSE_PROJECT_NAME`.
- `BUILDTRACK_POSTGRES_VOLUME`: the exact existing volume name, external and required; deploy refuses absent volumes.
- `BUILDTRACK_DATA_PATH`: existing shared data directory, absolute path, writable by UID/GID 1654 (.NET app user).
- `DAHUA_SDK_RUNTIME_PATH`: absolute directory containing Linux native runtime libraries, readable by UID 1654.
- Existing optional `DAHUA_ACTIVE_REGISTER_PASSWORD_OVERRIDE`, `DAHUA_CGI_PASSWORD`, `OPENAI_API_KEY` remain backend-only. No password is printed.

Admin/supervisor seed credentials are opt-in, without defaults. Production rejects supplied seed passwords shorter than 16 characters and known demo placeholders. Demo seeding is off by default; production compose pins password-reset and destructive demo-reset flags false. Re-seeding demo owners no longer changes existing hashes. No deploy performs credential rotation. Seed flags/data behavior otherwise remains unchanged.

Production CORS allows the five listed BuildTrack/Vercel origins. Set additional exact HTTPS preview origins explicitly if needed, never wildcard. With no CORS config outside Development, requests receive no permissive CORS headers.

## Exact migration from existing VPS (no database deletion)

Download the **successful** workflow deployment artifact for the target SHA on your workstation; transfer `buildtrack-deployment.tar.gz` to `/tmp/buildtrack-deployment.tar.gz` on VPS. Do not copy a source archive. Then run the following on VPS as root. It discovers existing mounts/Compose project from running containers, keeps PostgreSQL running, and never creates/replaces its volume.

```bash
sudo -i
set -euo pipefail
umask 077
OLD=/opt/ferstaclabs-buildtrack-dashboard
NEW=/opt/ferstaclabs-buildtrack
test -f "$OLD/.env"
test -f /tmp/buildtrack-deployment.tar.gz
export PROJECT=$(docker inspect -f '{{index .Config.Labels "com.docker.compose.project"}}' buildtrack-postgres)
export DBVOL=$(docker inspect buildtrack-postgres --format '{{range .Mounts}}{{if eq .Destination "/var/lib/postgresql/data"}}{{.Name}}{{end}}{{end}}')
export DATA=$(docker inspect buildtrack-api --format '{{range .Mounts}}{{if eq .Destination "/app/data"}}{{.Source}}{{end}}{{end}}')
test -n "$PROJECT"
test -n "$DBVOL"                         # stop here if the old database uses a bind, not a volume
docker volume inspect "$DBVOL" >/dev/null
test -d "$DATA"
SDK=$(docker inspect buildtrack-dahua-worker --format '{{range .Mounts}}{{if eq .Destination "/app/vendor/dahua-netsdk"}}{{.Source}}/linux-x64{{else if eq .Destination "/app/vendor/dahua-netsdk/linux-x64"}}{{.Source}}{{end}}{{end}}')
test -f "$SDK/libdhnetsdk.so"
mkdir -p /opt/buildtrack-runtime/linux-x64 "$NEW" /opt/buildtrack-backups
cp -a "$SDK/"*.so* /opt/buildtrack-runtime/linux-x64/
docker exec buildtrack-postgres sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "/opt/buildtrack-backups/pre-migration-$(date -u +%Y%m%dT%H%M%SZ).dump"
tar -xzf /tmp/buildtrack-deployment.tar.gz -C "$NEW"
cp "$OLD/.env" "$NEW/.env"
export NEW
python3 - <<'PY'
import os,re
from pathlib import Path
p=Path(os.environ['NEW'])/'.env'
text=p.read_text()
values={
 'COMPOSE_PROJECT_NAME':os.environ['PROJECT'],
 'BUILDTRACK_POSTGRES_VOLUME':os.environ['DBVOL'],
 'BUILDTRACK_DATA_PATH':os.environ['DATA'],
 'DAHUA_SDK_RUNTIME_PATH':'/opt/buildtrack-runtime/linux-x64',
 'BUILDTRACK_IMAGE_REGISTRY':'ghcr.io/ferstaclabs/ferstaclabs-buildtrack-dashboard',
 'BUILDTRACK_IMAGE_TAG':'main',
 'SEED_ADMIN_RESET_PASSWORD':'false',
 'SEED_BAKINITY_DEMO_RESET':'false',
 'SEED_SKYSNAP_DEMO_RESET':'false',
}
for key,value in values.items():
    text=re.sub(r'^'+key+r'=.*\n?', '', text, flags=re.M)
    text += '\n'+key+"='"+value+"'\n"
p.write_text(text)
PY
chmod 600 "$NEW/.env"
chmod +x "$NEW/deploy.sh" "$NEW/scripts/"*.sh
# Confirm required secrets exist with a secure editor; do not print .env.
# Compare all custom old compose overrides with new compose before proceeding.
# Pin BOTH images to a successful sha-<full SHA> tag in .env.
cd "$NEW"
docker compose --env-file .env -f docker-compose.prod.yml config --quiet
docker compose --env-file .env -f docker-compose.prod.yml pull
# Brief application downtime starts here; PostgreSQL stays running.
docker stop buildtrack-dahua-worker buildtrack-api
chown -R 1654:1654 "$DATA"
chmod -R u+rwX "$DATA"
chmod -R a+rX /opt/buildtrack-runtime/linux-x64
./deploy.sh
curl --fail http://127.0.0.1:8080/api/health
```

Do not delete the old directory or override files until migration and backups have been verified. The retained data path can still be inside the old directory: **never remove it**. No `docker compose down -v`, `docker volume prune`, new guessed volume name or automatic database restore. If the old database uses a bind mount, stop and adapt the production DB mount to that exact existing directory instead of creating an external volume. No firewall changes are made; 7000/9500 remain published.

If the old deployment has runtime flags in `docker-compose.override.yml`, explicitly carry their non-secret values into matching environment entries before cutover. A `.env` variable is only passed into a container when referenced by compose. The checked-in defaults preserve the current repository Active Register/identity behavior; this is not a claim to know uncommitted VPS overrides.

## Fresh VPS only

Only on a deliberately NEW installation create a new named volume (`docker volume create buildtrack-production-postgres`), use that exact name in `.env`, create a writable data directory owned by 1654, install the licensed SDK runtime, configure new secrets and optionally explicit initial admin credentials. Never run this volume-creation step during migration. Existing PostgreSQL volume ownership is NOT changed to 1654; only the application data directory is.

## Operation

```bash
cd /opt/ferstaclabs-buildtrack
sudo ./deploy.sh sha-<full-commit-SHA>   # update with immutable images
sudo ./deploy.sh                       # tag from .env
sudo ./deploy.sh sha-<previous-SHA>     # rollback application images
sudo BACKUP_DIRECTORY=/opt/buildtrack-backups BACKUP_RETENTION_DAYS=14 ./scripts/backup-db.sh
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs --tail=100 buildtrack-api buildtrack-dahua-worker
curl --fail http://127.0.0.1:8080/api/health
```

Treat logs as private, and redact identities/addresses before sharing. Deploy scripts do not print environments or passwords. The deploy script validates compose/storage/SDK, pulls first, records image IDs, stops worker, waits for API initialization/DB readiness, then starts worker. It uses a file lock, bounded health waits and best-effort previous-image rollback. First installation has no prior image to restore. Rollback does not downgrade schema or undo seed effects; back up before deployment and keep compatibility between releases. Running with an argument does not rewrite `.env`; pin the successful tag there for future default invocations.

API and worker use non-root runtime, read-only root filesystem, writable `/tmp` and explicit shared `/app/data`, dropped Linux capabilities, no-new-privileges, restart unless-stopped and rotated Docker logs. PostgreSQL remains postgres:16-alpine on the private Docker network. No enforced resource cap was guessed: monitor real SDK memory and set limits based on VPS capacity.

API health checks PostgreSQL connectivity after initialization. Worker health checks periodic DB access and hosted-service loop freshness (120s); it is not proof a terminal is connected. Existing Smart Event watchdog and DB diagnostics remain the source for subscription health. Docker marks unhealthy but does NOT automatically restart solely because of health status; deploy waits fail and rollback, and operators should monitor long-lived unhealthy containers.

Initialization is serialized by a bounded PostgreSQL advisory lock on a dedicated non-pooled session, covering all schema + seed work. Production worker additionally skips initialization and depends on healthy API. The database must already exist (official postgres image creates it on fresh volume). Failures propagate; lock connection is disposed on exception/cancellation, not swallowed.

## Validation and emergency checks

CI runs frontend/backend builds, unit tests and real-PostgreSQL initializer lock/concurrency tests before building images. Proprietary SDK load/export check runs on VPS before deployment because binaries are not redistributed to CI. If SDK preflight fails, preserve current containers and fix missing native dependencies first. If data permission check fails, verify the exact application data path/UID. If authentication fails after migration, check existing JWT/encryption/DB secrets, do not reset users. Public readiness remains `https://api.buildtrack.ferstaclabs.com/api/health`; Nginx still proxies 127.0.0.1:8080.
