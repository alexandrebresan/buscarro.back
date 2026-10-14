#!/usr/bin/env bash
set -euo pipefail
umask 077
# Arguments are directories, never shell expressions. Default to this deployment.
deployment=$(realpath -e -- "${1:-$(dirname "$0")/..}")
test -f "$deployment/compose.yaml"
backup_input=${2:-$deployment/backups}
mkdir -p -- "$backup_input"
backup_dir=$(realpath -e -- "$backup_input")
case "$backup_dir" in /|/home|/root|/var|/tmp) echo 'Unsafe backup directory' >&2; exit 2;; esac
exec 9>"$backup_dir/.backup.lock"
flock -n 9 || { echo 'Another backup is running' >&2; exit 1; }
temporary=$(mktemp "$backup_dir/.dump-XXXXXX")
trap 'rm -f -- "$temporary"' EXIT
cd -- "$deployment"
docker compose exec -T postgres sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$temporary"
test -s "$temporary"
archive="$backup_dir/buscarro-$(date -u +%Y%m%dT%H%M%SZ).dump"
test ! -e "$archive" || { echo 'Archive already exists' >&2; exit 1; }
mv -- "$temporary" "$archive"
# Delete only our dated archives inside the resolved directory, after success.
mapfile -t archives < <(find "$backup_dir" -maxdepth 1 -type f -name 'buscarro-????????T??????Z.dump' -printf '%f\n' | LC_ALL=C sort -r)
for ((index=7; index<${#archives[@]}; index++)); do
    file=${archives[index]}
    [[ "$file" =~ ^buscarro-[0-9]{8}T[0-9]{6}Z\.dump$ ]] || continue
    rm -f -- "$backup_dir/$file"
done
printf 'Backup complete: %s\n' "$archive"
