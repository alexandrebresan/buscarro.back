#!/usr/bin/env bash
# Transfer only the image bundle and its receiver over a pinned SSH connection.
set +x
set -euo pipefail
umask 077

fail() { printf 'Image transfer: %s\n' "$1" >&2; exit 1; }
[[ $# == 1 && -d $1 ]] || fail 'expected one existing package directory'
package_dir=$(cd -- "$1" && pwd -P)
repo_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd -P)
for file in images.tar.gz images.sha256 deploy-images.sh; do
  [[ -f "$package_dir/$file" && ! -L "$package_dir/$file" ]] || fail 'package is missing a regular allowlisted file'
done

[[ ${DEPLOY_SHA:-} =~ ^[0-9a-f]{40}$ ]] || fail 'invalid deployment SHA'
[[ ${EC2_USER:-} =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || fail 'invalid SSH user'
[[ ${EC2_HOST:-} =~ ^100\.([0-9]{1,3})\.([0-9]{1,3})\.([0-9]{1,3})$ ]] || fail 'host must be a Tailscale IPv4 address'
octet2=${BASH_REMATCH[1]} octet3=${BASH_REMATCH[2]} octet4=${BASH_REMATCH[3]}
for octet in "$octet2" "$octet3" "$octet4"; do
  [[ $octet == 0 || $octet != 0* ]] || fail 'host must use canonical IPv4 octets'
done
(( 10#$octet2 >= 64 && 10#$octet2 <= 127 && 10#$octet3 <= 255 && 10#$octet4 <= 255 )) || fail 'host is outside the Tailscale address range'
[[ ${EC2_DEPLOY_PATH:-} =~ ^/[a-zA-Z0-9_./-]+$ && $EC2_DEPLOY_PATH != / ]] || fail 'invalid absolute deployment path'
[[ ! $EC2_DEPLOY_PATH =~ (^|/)\.\.?(/|$) && $EC2_DEPLOY_PATH != *//* ]] || fail 'deployment path contains an unsafe component'
[[ ${EC2_HEALTH_URL:-} =~ ^https://([a-zA-Z0-9.-]+)/health/ready$ ]] || fail 'invalid HTTPS readiness URL'
health_host=${BASH_REMATCH[1]}
[[ $health_host == *.* && ${#health_host} -le 253 ]] || fail 'invalid readiness hostname'
IFS=. read -r -a health_labels <<< "$health_host"
[[ $health_host != *. ]] || fail 'invalid readiness hostname'
for label in "${health_labels[@]}"; do
  [[ $label =~ ^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?$ ]] || fail 'invalid readiness hostname'
done
[[ -n ${EC2_SSH_KEY:-} ]] || fail 'SSH private key is missing'
[[ ${EC2_SSH_FINGERPRINT:-} =~ ^SHA256:[A-Za-z0-9+/]{43}$ ]] || fail 'invalid trusted SSH fingerprint'

temp_parent=${RUNNER_TEMP:-${TMPDIR:-/tmp}}
[[ -d $temp_parent ]] || fail 'temporary directory does not exist'
temp_parent=$(cd -- "$temp_parent" && pwd -P)
for protected_dir in "$package_dir" "$repo_dir"; do
  [[ $temp_parent != "$protected_dir" && $temp_parent != "$protected_dir/"* ]] || fail 'temporary secrets must be outside the repository and package'
done
temp_dir=$(mktemp -d "$temp_parent/buscarro-transfer.XXXXXXXX")
key_file="$temp_dir/id_ed25519"
known_hosts="$temp_dir/known_hosts"
cleanup() {
  rm -f -- "$key_file" "$known_hosts"
  rmdir -- "$temp_dir"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
printf '%s\n' "$EC2_SSH_KEY" > "$key_file"
chmod 600 "$key_file"
ssh-keyscan -T 10 -t ed25519 "$EC2_HOST" > "$known_hosts"
fingerprints=$(ssh-keygen -lf "$known_hosts" -E sha256)
[[ -n $fingerprints && $fingerprints != *$'\n'* ]] || fail 'expected exactly one SSH host key'
read -r _ fingerprint _ <<< "$fingerprints"
[[ $fingerprint == "$EC2_SSH_FINGERPRINT" ]] || fail 'SSH host fingerprint does not match the trusted value'

ssh_options=(
  -i "$key_file"
  -o BatchMode=yes
  -o StrictHostKeyChecking=yes
  -o "UserKnownHostsFile=$known_hosts"
  -o IdentitiesOnly=yes
  -o ConnectTimeout=15
  -o ForwardAgent=no
  -o PasswordAuthentication=no
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=3
)
incoming="${EC2_DEPLOY_PATH%/}/.incoming/$DEPLOY_SHA"
target="$EC2_USER@$EC2_HOST"
# The strict validation above excludes quotes and all shell metacharacters.
ssh "${ssh_options[@]}" "$target" "mkdir -p -- '$incoming'"
scp "${ssh_options[@]}" -- \
  "$package_dir/images.tar.gz" \
  "$package_dir/images.sha256" \
  "$package_dir/deploy-images.sh" \
  "$target:$incoming/"
ssh "${ssh_options[@]}" "$target" \
  "bash '$incoming/deploy-images.sh' '$EC2_DEPLOY_PATH' '$incoming' '$DEPLOY_SHA' '$EC2_HEALTH_URL'"
