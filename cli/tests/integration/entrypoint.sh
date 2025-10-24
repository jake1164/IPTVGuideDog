#!/usr/bin/env bash

set -euo pipefail

: "${WORKSPACE:=/workspace}"
: "${DOTNET_CONFIGURATION:=Debug}"

CLI_DIR="${WORKSPACE}/cli"
INTEGRATION_DIR="${CLI_DIR}/tests/integration"
RUNTIME_DIR="${INTEGRATION_DIR}/runtime"
OUTPUT_ROOT_DEFAULT="${TEST_OUTPUT_DIR:-${WORKSPACE}/test-output}"
LEGACY_FILTER="${INTEGRATION_DIR}/legacy/m3u-filter.py"
MATRIX_PATH="${MATRIX_PATH:-${INTEGRATION_DIR}/matrix.json}"
CONFIG_TEMPLATE="${INTEGRATION_DIR}/fixtures/test-config.template.yaml"
GENERATED_CONFIG="${RUNTIME_DIR}/test-config.yaml"

log() {
  printf '[entrypoint] %s\n' "$*" >&2
}

die() {
  log "$1"
  exit 1
}

ensure_paths() {
  [[ -d "${CLI_DIR}" ]] || die "Expected CLI directory at ${CLI_DIR} (mount the repo to /workspace)."
  [[ -s "${LEGACY_FILTER}" ]] || die "Legacy Python filter not found or empty at ${LEGACY_FILTER}."
  [[ -f "${MATRIX_PATH}" ]] || die "Matrix file not found at ${MATRIX_PATH}."
  [[ -f "${CONFIG_TEMPLATE}" ]] || die "Config template missing at ${CONFIG_TEMPLATE}."
  mkdir -p "${OUTPUT_ROOT_DEFAULT}" "${RUNTIME_DIR}"
}

build_cli() {
  log "Restoring and building iptv CLI (configuration: ${DOTNET_CONFIGURATION})."
  dotnet restore "${CLI_DIR}/iptv/iptv.csproj" >/dev/null
  dotnet build "${CLI_DIR}/iptv/iptv.csproj" -c "${DOTNET_CONFIGURATION}" --no-restore >/dev/null
}

render_config() {
  local output_root="$1"
  local provider_name="$2"
  python3 - <<'PY' "${CONFIG_TEMPLATE}" "${GENERATED_CONFIG}" "${output_root}" "${WORKSPACE}" "${provider_name}"
import sys
from pathlib import Path

template_path = Path(sys.argv[1])
target_path = Path(sys.argv[2])
output_root = Path(sys.argv[3])
workspace = Path(sys.argv[4])
provider = sys.argv[5] or "default"

text = template_path.read_text(encoding="utf-8")
legacy_dir = Path("cli/tests/integration/legacy")
replacements = {
    "{{OUTPUT_ROOT}}": str(output_root),
    "{{WORKSPACE}}": str(workspace),
    "{{DROP_FILE}}": str(workspace / legacy_dir / "sample_drop_list.txt"),
    "{{PROVIDER}}": provider,
}
for placeholder, value in replacements.items():
    text = text.replace(placeholder, value)

target_path.write_text(text, encoding="utf-8")
PY
}

run_suite() {
  local provider_name="$1"
  local env_file="$2"
  local output_root="${OUTPUT_ROOT_DEFAULT}/${provider_name}"

  if [[ -n "${env_file}" && "${env_file}" != /* ]]; then
    env_file="${WORKSPACE}/${env_file}"
  fi

  render_config "${output_root}" "${provider_name}"

  local python_script="${INTEGRATION_DIR}/run_matrix.py"
  [[ -f "${python_script}" ]] || die "Missing matrix runner at ${python_script}."

  log "Executing integration matrix for provider '${provider_name}' with output root ${output_root}."

  if [[ -n "${env_file}" ]]; then
    if [[ -f "${env_file}" ]]; then
      log "Loading provider environment overrides from ${env_file}."
    else
      die "Provider env file not found: ${env_file}"
    fi
  fi

  env \
    PROVIDER_NAME="${provider_name}" \
    PROVIDER_ENV_FILE="${env_file}" \
    TEST_OUTPUT_DIR="${output_root}" \
    python3 "${python_script}" \
      --workspace "${WORKSPACE}" \
      --matrix "${MATRIX_PATH}" \
      --output "${output_root}" \
      --configuration "${DOTNET_CONFIGURATION}" \
      --provider "${provider_name}"
}

main() {
  ensure_paths
  build_cli

  local provider_files_raw="${PROVIDER_ENV_FILES:-}"
  if [[ -n "${provider_files_raw}" ]]; then
    log "Running suite for multiple providers: ${provider_files_raw}"
    local IFS=','
    for entry in ${provider_files_raw}; do
      entry="$(echo "${entry}" | xargs)"
      [[ -n "${entry}" ]] || continue
      local provider_name provider_env
      provider_name="$(basename "${entry}")"
      provider_name="${provider_name%.env}"
      provider_env="${entry}"
      run_suite "${provider_name}" "${provider_env}"
    done
  else
    local provider_name="${PROVIDER_NAME:-local}"
    local env_file="${PROVIDER_ENV_FILE:-}"
    run_suite "${provider_name}" "${env_file}"
  fi

  log "Integration suite completed successfully."
}

main "$@"
