#!/usr/bin/env bash
set -euo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ -f .env ]] || { echo 'Missing .env' >&2; exit 1; }
command -v python3 >/dev/null
command -v flock >/dev/null
exec 9>.deploy.lock
flock -n 9 || { echo 'Another deployment is running' >&2; exit 1; }
if [[ $# -gt 0 ]]; then
  [[ "$1" =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$ ]] || { echo 'Invalid image tag' >&2; exit 1; }
  export BUILDTRACK_IMAGE_TAG="$1"
fi
dc=(docker compose --env-file .env -f docker-compose.prod.yml)
config=$(mktemp)
rollback=$(mktemp)
changed=false
cleanup() { rm -f "$config" "$rollback"; }
trap cleanup EXIT
"${dc[@]}" config --format json > "$config"
read_config() { python3 - "$config" "$1" <<'PY'
import json,sys
value=json.load(open(sys.argv[1]))
for key in sys.argv[2].split('.'):
    value=value[key]
print(value)
PY
}
volume=$(read_config volumes.buildtrack-postgres.name)
project=$(read_config name)
for container in buildtrack-api buildtrack-dahua-worker buildtrack-postgres; do
  if docker inspect "$container" >/dev/null 2>&1; then
    owner=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' "$container")
    [[ "$owner" == "$project" ]] || { echo 'Existing container belongs to another Compose project; check COMPOSE_PROJECT_NAME' >&2; exit 1; }
  fi
done
docker volume inspect "$volume" >/dev/null || { echo 'Required existing database volume missing; refusing to create an empty database' >&2; exit 1; }
"${dc[@]}" pull
worker_image=$(read_config services.buildtrack-dahua-worker.image)
sdk=$(python3 - "$config" <<'PY'
import json,sys
v=json.load(open(sys.argv[1]))['services']['buildtrack-dahua-worker']['volumes']
print(next(x['source'] for x in v if x['target']=='/app/vendor/dahua-netsdk/linux-x64'))
PY
)
[[ -f "$sdk/libdhnetsdk.so" ]] || { echo 'Minimal Dahua runtime SDK is missing' >&2; exit 1; }
# Verify dependencies/exports without opening listener ports or connecting to devices.
docker run --rm --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges \
  --mount "type=bind,src=$sdk,dst=/app/vendor/dahua-netsdk/linux-x64,readonly" "$worker_image" --sdk-check
"${dc[@]}" run --rm --no-deps --entrypoint sh buildtrack-api -c 'test -w /app/data'
mkdir -p deployment-history
stamp=$(date -u +%Y%m%dT%H%M%SZ)
# Record only immutable image IDs, never container environment or credentials.
printf 'services:\n' > "$rollback"
previous=true
for service in buildtrack-api buildtrack-dahua-worker; do
  id=$("${dc[@]}" ps -aq "$service")
  if [[ -z "$id" ]]; then previous=false; continue; fi
  image=$(docker inspect --format '{{.Image}}' "$id")
  printf '  %s:\n    image: %s\n' "$service" "$image" >> "$rollback"
done
cp "$rollback" "deployment-history/$stamp.previous-images.yml"
recover() {
  code=$?
  trap - ERR
  echo 'Deployment failed. See service logs; secrets have not been printed.' >&2
  if $changed && $previous; then
    echo 'Restoring previous API and worker images (database is never rolled back automatically).' >&2
    rollback_dc=("${dc[@]}" -f "$rollback")
    "${rollback_dc[@]}" stop buildtrack-dahua-worker || true
    "${rollback_dc[@]}" up -d --no-deps --pull never --wait --wait-timeout 300 buildtrack-api || true
    "${rollback_dc[@]}" up -d --no-deps --pull never --wait --wait-timeout 300 buildtrack-dahua-worker || true
  fi
  exit "$code"
}
trap recover ERR
"${dc[@]}" up -d --wait --wait-timeout 120 postgres
changed=true
"${dc[@]}" stop buildtrack-dahua-worker
"${dc[@]}" up -d --no-deps --wait --wait-timeout 600 buildtrack-api
"${dc[@]}" up -d --no-deps --wait --wait-timeout 300 buildtrack-dahua-worker
printf 'services:\n' > "deployment-history/$stamp.deployed-images.yml"
for service in buildtrack-api buildtrack-dahua-worker; do
  id=$("${dc[@]}" ps -q "$service")
  image=$(docker inspect --format '{{.Image}}' "$id")
  printf '  %s:\n    image: %s\n' "$service" "$image" >> "deployment-history/$stamp.deployed-images.yml"
done
echo "Deployment healthy. Image history: deployment-history/$stamp.deployed-images.yml"
