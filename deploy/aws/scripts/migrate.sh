#!/usr/bin/env bash
set -euo pipefail
: "${PGHOST:?}" "${PGDATABASE:?}" "${PGUSER:?}" "${PGPASSWORD:?}"
exec psql -X --set=ON_ERROR_STOP=1 --file=/migrations.sql
