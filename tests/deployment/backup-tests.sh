#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
backup="$root/deploy/aws/scripts/backup.sh"
test -f "$backup" || { echo 'RED: backup implementation missing'; exit 1; }
tmp=$(mktemp -d)
# Temporary fixture is retained for inspection; no recursive delete.
mkdir -p "$tmp/bin" "$tmp/deployment" "$tmp/backups"
printf 'services: {}\n' > "$tmp/deployment/compose.yaml"
cat > "$tmp/bin/docker" <<'STUB'
#!/usr/bin/env bash
printf 'archive-data'
exit "${STUB_EXIT:-0}"
STUB
cat > "$tmp/bin/flock" <<'STUB'
#!/usr/bin/env bash
exit 0
STUB
chmod +x "$tmp/bin/docker" "$tmp/bin/flock"
export PATH="$tmp/bin:$PATH"
export STUB_EXIT=1
printf original > "$tmp/backups/buscarro-20200101T000000Z.dump"
if bash "$backup" "$tmp/deployment" "$tmp/backups"; then echo 'failed dump accepted'; exit 1; fi
test "$(cat "$tmp/backups/buscarro-20200101T000000Z.dump")" = original
test "$(find "$tmp/backups" -name '*.dump' | wc -l)" -eq 1
test "$(find "$tmp/backups" -name '.dump-*' | wc -l)" -eq 0
for n in 01 02 03 04 05 06 07 08; do printf old > "$tmp/backups/buscarro-202001${n}T000000Z.dump"; done
printf keep > "$tmp/backups/unrelated.dump"
export STUB_EXIT=0
bash "$backup" "$tmp/deployment" "$tmp/backups"
test "$(find "$tmp/backups" -name 'buscarro-*.dump' | wc -l)" -eq 7
test -f "$tmp/backups/unrelated.dump"
test ! -f "$tmp/backups/buscarro-20200101T000000Z.dump"
echo 'PASS: failure preserves archives; successful dump retains seven and unrelated files'
