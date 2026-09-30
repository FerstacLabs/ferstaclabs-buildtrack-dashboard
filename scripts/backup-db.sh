#!/usr/bin/env bash
set -euo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dir=${BACKUP_DIRECTORY:-./backups}
retention=${BACKUP_RETENTION_DAYS:-14}
[[ "$retention" =~ ^[1-9][0-9]*$ ]] || { echo 'Retention must be positive days' >&2; exit 1; }
mkdir -p "$dir"
file="$dir/buildtrack-$(date -u +%Y%m%dT%H%M%SZ)-$$.dump"
trap 'rm -f "$file.partial"' EXIT
docker compose --env-file .env -f docker-compose.prod.yml exec -T postgres \
  sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$file.partial"
[[ -s "$file.partial" ]]
mv "$file.partial" "$file"
find "$dir" -maxdepth 1 -type f -name 'buildtrack-*.dump' -mtime "+$retention" -delete
echo "Database backup saved: $file"
