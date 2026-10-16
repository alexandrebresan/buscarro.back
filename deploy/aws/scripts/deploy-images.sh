#!/usr/bin/env bash
# Receives images built in GitHub; never builds, migrates or seeds the database.
set -euo pipefail
log() { printf '[deploy-images] %s\n' "$*" >&2; }
die() { log "$*"; exit 1; }
[[ $# == 4 ]] || die 'Expected deployment_dir incoming_dir sha health_url.'
deployment_input=$1 incoming_input=$2 sha=$3 health_url=$4
[[ $sha =~ ^[0-9a-f]{40}$ ]] || die 'Invalid commit SHA.'
[[ $health_url =~ ^https://[^/?#[:space:]]+/health/ready$ ]] || die 'Expected an HTTPS /health/ready URL.'
deployment=$(realpath -e -- "$deployment_input") || die 'Deployment directory is missing.'
[[ -d $deployment && -f $deployment/compose.yaml ]] || die 'Existing compose.yaml required.'
incoming=$(realpath -e -- "$incoming_input") || die 'Incoming directory is missing.'
[[ -d $incoming && ! -L $incoming_input && $incoming == "$deployment/.incoming/$sha" ]] || die 'Incoming path must be deployment/.incoming/SHA without symlinks.'
[[ $(realpath -e -- "$deployment/.incoming") == "$deployment/.incoming" ]] || die 'Incoming parent cannot be a symlink.'
cd -- "$deployment"
exec 9> .deploy.lock
flock -n 9 || die 'Another deployment holds the lock.'
DOCKER=(sudo -n docker)
COMPOSE=("${DOCKER[@]}" compose --project-name buscarro)
header='# Managed by Buscarro automatic image deploy.'
override="$deployment/compose.override.yaml"
backup=''
temporary=''
changed=false
complete=false
old_api=''
old_worker=''
write_override() {
  local api=$1 worker=$2
  temporary=$(mktemp "$deployment/.compose.override.XXXXXX")
  printf '%s\nservices:\n  api:\n    image: %s\n  worker:\n    image: %s\n' "$header" "$api" "$worker" > "$temporary"
  mv -f -- "$temporary" "$override"
  temporary=''
}
verify_readiness() {
  curl --fail --silent --show-error --output /dev/null --connect-timeout 5 --max-time 10 \
    --retry 10 --retry-delay 2 --retry-max-time 120 --retry-all-errors "$health_url" || die 'API readiness check failed.'
  worker_container=$("${COMPOSE[@]}" ps -q worker)
  [[ -n $worker_container && $("${DOCKER[@]}" inspect --format '{{.State.Running}}' "$worker_container") == true ]] || die 'Worker is not running.'
}
finish() {
  local result=$?
  trap - EXIT
  if [[ $changed == true && $complete != true ]]; then
    log 'Deployment failed; restoring previous API and worker.'
    if [[ -n $backup ]]; then
      temporary=$(mktemp "$deployment/.compose.override.XXXXXX")
      if cp -- "$backup" "$temporary" && mv -f -- "$temporary" "$override"; then temporary=''; else log 'ROLLBACK FAILED: could not restore override.'; fi
    else
      write_override "$old_api" "$old_worker" || log 'ROLLBACK FAILED: could not write previous image refs.'
    fi
    if "${COMPOSE[@]}" up -d --no-deps --pull never api worker; then
      log 'Previous API and worker restored.'
    else
      log 'ROLLBACK FAILED: previous services could not be started; operator action required.'
    fi
    result=1
  fi
  [[ -z $backup ]] || rm -f -- "$backup"
  [[ -z $temporary ]] || rm -f -- "$temporary"
  # Only these uploaded files are disposable; retain directory and deployment logs.
  rm -f -- "$incoming/images.tar.gz" "$incoming/images.sha256" "$incoming/deploy-images.sh"
  exit "$result"
}
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
for filename in images.tar.gz images.sha256 deploy-images.sh; do
  [[ -f $incoming/$filename && ! -L $incoming/$filename ]] || die "Missing or symlinked upload: $filename."
done
if [[ -e $override || -L $override ]]; then
  [[ -f $override && ! -L $override && $(head -n 1 "$override") == "$header" ]] || die 'Custom compose.override.yaml must be resolved manually.'
  backup=$(mktemp "$deployment/.override.backup.XXXXXX")
  cp -- "$override" "$backup"
fi
# Do not permit a checksum manifest to select arbitrary host files.
manifest=$(cat "$incoming/images.sha256")
[[ $manifest =~ ^([0-9a-f]{64})[[:space:]][\ \*]images\.tar\.gz$ ]] || die 'Invalid checksum manifest.'
expected_hash=${BASH_REMATCH[1]}
actual_hash=$(sha256sum -- "$incoming/images.tar.gz")
[[ ${actual_hash%% *} == "$expected_hash" ]] || die 'Image archive checksum mismatch.'
"${COMPOSE[@]}" config -q || die 'Existing Compose configuration is invalid.'
api_container=$("${COMPOSE[@]}" ps -q api)
worker_container=$("${COMPOSE[@]}" ps -q worker)
[[ -n $api_container && -n $worker_container && $api_container != *$'\n'* && $worker_container != *$'\n'* ]] || die 'Exactly one existing API and worker container are required.'
old_api=$("${DOCKER[@]}" inspect --format '{{.Config.Image}}' "$api_container")
old_worker=$("${DOCKER[@]}" inspect --format '{{.Config.Image}}' "$worker_container")
# These characters cover Docker named and digest refs and cannot inject YAML.
[[ $old_api =~ ^[a-zA-Z0-9][a-zA-Z0-9._/@:-]*$ && $old_worker =~ ^[a-zA-Z0-9][a-zA-Z0-9._/@:-]*$ ]] || die 'Previous image refs are unsafe for rollback.'
"${DOCKER[@]}" load -i "$incoming/images.tar.gz" || die 'Image load failed; current services are unchanged.'
new_api="buscarro-api:$sha"
new_worker="buscarro-worker:$sha"
for image_ref in "$new_api" "$new_worker"; do
  architecture=$("${DOCKER[@]}" image inspect --format '{{.Os}}/{{.Architecture}}' "$image_ref")
  [[ $architecture == linux/arm64 ]] || die 'Uploaded images must be linux/arm64.'
done
if [[ $old_api == "$new_api" && $old_worker == "$new_worker" ]]; then
  log 'Requested commit is already running; no restart required.'
  verify_readiness
  complete=true
  exit 0
fi
changed=true
write_override "$new_api" "$new_worker"
"${COMPOSE[@]}" config -q || die 'Updated Compose configuration is invalid.'
"${COMPOSE[@]}" up -d --no-deps --pull never api worker || die 'API/worker update failed.'
verify_readiness
complete=true
log "Deployment completed: $sha."
