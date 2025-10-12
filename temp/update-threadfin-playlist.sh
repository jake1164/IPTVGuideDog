#!/usr/bin/env bash
# Update & filter M3U for Threadfin (reads .env for creds)
set -euo pipefail
umask 022

SCRIPTS_DIR="/etc/docker-apps/threadfin/scripts"
ENV_FILE="${SCRIPTS_DIR}/.env"

# ---- Load .env like Docker (simple KEY=VAL lines) ----
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a
fi

# ────────── CONFIG (edit these) ──────────
# Template URL that uses env vars from .env
SRC_URL_TEMPLATE='http://live.tv/get.php?username=${USER}&password=${PASS}&type=m3u_plus&output=ts'

DROP_FILE="${SCRIPTS_DIR}/remove.txt"      # groups to DROP (one per line, '#' comments OK)
FILTER="${SCRIPTS_DIR}/m3u-filter.py"           # python filter
OUT_DIR="/etc/docker-apps/threadfin/conf"
OUT_FILE="${OUT_DIR}/outfile.m3u"
LOG_FILE="${SCRIPTS_DIR}/playlist.log"
LOCK_FILE="/var/lock/update-threadfin-playlist.lock"

# OPTIONAL: also pull XMLTV to feed Threadfin
EPG_URL_TEMPLATE='http://live.tv/xmltv.php?username=${USER}&password=${PASS}'
EPG_OUT_FILE="${OUT_DIR}/outfile.xml"

# Optional Prometheus textfile collector
METRICS_FILE="/var/lib/node_exporter/textfile_collector/threadfin_m3u.prom"

# NEW: URL-kind filter for the Python script (default: live only).
# Override in .env if desired, e.g. TYPE_FILTER="movie,series" or empty to disable.
TYPE_FILTER="${TYPE_FILTER:-live}"
# ─────────────────────────────────────────

PY="${PYTHON_BIN:-/usr/bin/python3}"

mkdir -p "${OUT_DIR}" "$(dirname "$LOCK_FILE")"
touch "${LOG_FILE}"

# prevent overlapping runs
exec 9>"${LOCK_FILE}"
if ! flock -n 9; then
  echo "$(date -Is)  SKIP  previous run still in progress" >> "${LOG_FILE}"
  exit 0
fi

ts() { date -Is; }
count_channels() { [[ -s "$1" ]] && grep -c '^#EXTINF' "$1" || echo 0; }

fetch_epg() {
  local url_tmpl="${EPG_URL_TEMPLATE:-}"
  local out="${EPG_OUT_FILE:-}"
  [[ -z "$url_tmpl" || -z "$out" ]] && return 0

  local url tmp
  url="$(eval "echo \"$url_tmpl\"")" || return 1
  tmp="${out}.tmp"

  # First try: HTTP/1.1 + retries, tolerate transient server issues
  if curl -fsSL --http1.1 \
      --retry 5 --retry-delay 2 --retry-all-errors \
      --connect-timeout 15 --max-time 300 \
      -A "curl/8 threadfin-epg" \
      "$url" -o "$tmp"; then
    :
  else
    # Second try: plain retry without http1.1 flag
    if ! curl -fsSL \
        --retry 5 --retry-delay 2 --retry-all-errors \
        --connect-timeout 15 --max-time 300 \
        -A "curl/8 threadfin-epg" \
        "$url" -o "$tmp"; then
      echo "$(ts)  WARN  epg_fetch_failed (URL redacted)" >> "${LOG_FILE}"
      rm -f "$tmp"
      return 1
    fi
  fi

  # Basic sanity: non-empty and looks like XMLTV
  if [[ -s "$tmp" ]] && grep -q "<tv" "$tmp"; then
    mv "$tmp" "$out"
    chmod 644 "$out" || true
    local bytes
    bytes=$(stat -c%s "$out" 2>/dev/null || wc -c <"$out")
    echo "$(ts)  OK    epg_refreshed size=${bytes}" >> "${LOG_FILE}"
    return 0
  fi

  echo "$(ts)  WARN  epg_fetch_invalid_content (URL redacted)" >> "${LOG_FILE}"
  rm -f "$tmp"
  return 1
}

fetch_m3u() {
  local url_tmpl="${SRC_URL_TEMPLATE:-}"
  local out="${OUT_FILE:-}"
  [[ -z "$url_tmpl" || -z "$out" ]] && return 1

  local url tmp
  url="$(eval "echo \"$url_tmpl\"")" || return 1
  tmp="${out}.m3u.tmp"

  # First try: HTTP/1.1 (some portals are flaky on HTTP/2)
  if curl -fsSL --http1.1 \
      --retry 5 --retry-delay 3 --retry-all-errors \
      --connect-timeout 15 --max-time 300 \
      -A "curl/8 threadfin-m3u" \
      "$url" -o "$tmp"; then
    :
  else
    # Second attempt without forcing http1.1
    if ! curl -fsSL \
         --retry 5 --retry-delay 3 --retry-all-errors \
         --connect-timeout 15 --max-time 300 \
         -A "curl/8 threadfin-m3u" \
         "$url" -o "$tmp"; then
      echo "$(ts)  WARN  m3u_fetch_failed (URL redacted)" >> "${LOG_FILE}"
      rm -f "$tmp"
      return 1
    fi
  fi

  # Basic sanity: must contain EXTINF lines
  if grep -q '^#EXTINF' "$tmp"; then
    echo "$tmp"
    return 0
  fi

  echo "$(ts)  WARN  m3u_fetch_invalid_content (URL redacted)" >> "${LOG_FILE}"
  rm -f "$tmp"
  return 1
}

# --- Sanity: ensure we have credentials (at least PASS must be present) ---
if [[ -z "${PASS:-}" ]]; then
  echo "$(ts)  ERROR missing PASS in .env; refusing to run" >> "${LOG_FILE}"
  if [[ -d "$(dirname "$METRICS_FILE")" ]]; then
    cat > "${METRICS_FILE}" <<EOF
# HELP threadfin_m3u_update_success 1 if last update succeeded, 0 otherwise
# TYPE threadfin_m3u_update_success gauge
threadfin_m3u_update_success 0
EOF
  fi
  exit 1
fi

prev_count=$(count_channels "${OUT_FILE}")

# Fetch M3U to a temp file first (robust retries); keep last good OUT_FILE on failure
TMP_SRC="$(fetch_m3u)" || {
  echo "$(ts)  ERROR m3u_prefetch_failed; keeping existing ${OUT_FILE}" >> "${LOG_FILE}"
  if [[ -d "$(dirname "$METRICS_FILE")" ]]; then
    cat > "${METRICS_FILE}" <<EOF
# HELP threadfin_m3u_update_success 1 if last update succeeded, 0 otherwise
# TYPE threadfin_m3u_update_success gauge
threadfin_m3u_update_success 0
EOF
  fi
  exit 1
}

# Run the filter using the local temp M3U as input
set +e
FILTER_ARGS=( --ignore-case )

# Include DROP list if present
[[ -s "$DROP_FILE" ]] && FILTER_ARGS+=( --drop-file "$DROP_FILE" )

# NEW: apply URL-kind filter (default is 'live'; set TYPE_FILTER="" to disable)
if [[ -n "${TYPE_FILTER}" ]]; then
  FILTER_ARGS+=( --type "${TYPE_FILTER}" )
fi

RESULT="$("$PY" "$FILTER" filter "$TMP_SRC" "$OUT_FILE" "${FILTER_ARGS[@]}" 2>&1)"
RC=$?
set -e
rm -f "$TMP_SRC"

if [[ $RC -ne 0 ]]; then
  echo "$(ts)  ERROR rc=$RC filter_failed: $(printf '%s' "$RESULT" | head -n 1 | cut -c1-200)" >> "${LOG_FILE}"
  if [[ -d "$(dirname "$METRICS_FILE")" ]]; then
    cat > "${METRICS_FILE}" <<EOF
# HELP threadfin_m3u_update_success 1 if last update succeeded, 0 otherwise
# TYPE threadfin_m3u_update_success gauge
threadfin_m3u_update_success 0
EOF
  fi
  exit $RC
fi

new_count=$(count_channels "${OUT_FILE}")

# Optionally pull EPG
fetch_epg || true

delta=$(( new_count - prev_count ))
removed=$(( prev_count > 0 ? prev_count - new_count : 0 ))
echo "$(ts)  OK    kept=${new_count}  prev=${prev_count}  delta=${delta}  removed=${removed}  type_filter=${TYPE_FILTER:-none}" >> "${LOG_FILE}"

# ensure readable by Threadfin/container
chmod 644 "$OUT_FILE"
[[ -n "${EPG_OUT_FILE:-}" && -f "$EPG_OUT_FILE" ]] && chmod 644 "$EPG_OUT_FILE"

# Export metrics (success)
if [[ -d "$(dirname "$METRICS_FILE")" ]]; then
  cat > "${METRICS_FILE}" <<EOF
# HELP threadfin_m3u_update_success 1 if last update succeeded, 0 otherwise
# TYPE threadfin_m3u_update_success gauge
threadfin_m3u_update_success 1
# HELP threadfin_m3u_channels Number of channels in filtered M3U
# TYPE threadfin_m3u_channels gauge
threadfin_m3u_channels ${new_count}
# HELP threadfin_m3u_removed Last run removed channel count (negative values clipped to 0)
# TYPE threadfin_m3u_removed gauge
threadfin_m3u_removed ${removed}
EOF
fi

exit 0
