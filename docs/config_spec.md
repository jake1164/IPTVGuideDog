# IPTV Config – Schema

This document defines the **shared configuration schema** used by the CLI, Blazor Server app, and Docker image. Keep it source‑of‑truth so all entry points behave identically.

- **Formats supported:** YAML (preferred) and JSON
- **Profiles:** Multiple profiles may be defined; select one via CLI `--profile` or server settings
- **Env substitution:** `${ENV_VAR}` placeholders inside string fields will be expanded

---

## Top-Level Structure
```yaml
profiles:
  <profileName>:
    inputs: { ... }
    filters: { ... }
    mapping: { ... }
    output: { ... }
    logging: { ... }
```

> **Required keys per profile:** `inputs`, `output`. Others are optional with defaults.

---

## 1) `inputs`
Holds source locations and download settings for playlist and EPG.

```yaml
inputs:
  playlist:
    url: "${PROVIDER_PLAYLIST_URL}"   # string, required
    headers:                           # map<string,string>, optional
      User-Agent: "IPTVGuideDog/1.0"
      Authorization: "Bearer ${PLAYLIST_TOKEN}"  # if needed
    timeoutSeconds: 30                 # int, optional (default 30)
    retries: 3                         # int, optional (default 3)
    maxDownloadMb: 200                 # int, optional; safety cap

  epg:
    url: "${PROVIDER_EPG_URL}"        # string, optional (omit to skip)
    headers: {}
    allowCompressed: true              # bool, optional (default true) – autodetect .gz/.zip
    timeoutSeconds: 60
    retries: 3
    maxDownloadMb: 500
```

**Validation**
- `playlist.url` must be a valid `http(s)` URL
- `epg.url` optional; if absent, EPG stage is skipped
- `timeoutSeconds` ∈ [5, 600]
- `retries` ∈ [0, 10]

---

## 2) `filters`
Declarative rules for trimming channels.

```yaml
filters:
  includeGroups: []                     # list<string>; if non-empty, only these groups are kept
  excludeGroups: []                     # list<string>; removed even if included elsewhere
  excludeTitleRegex: "(?i)\\b(4k|uhd)\\b"  # string; .NET regex; optional
  dropListFile: "/config/drop-groups.txt"   # path to LF/CRLF text file, one group per line; optional
```

**Notes**
- Matching is **exact** for group names (case-sensitive by default). Consider normalizing names in `mapping` if needed.
- Regex uses .NET engine. Invalid regex → config validation error.

---

## 3) `mapping`
Lightweight normalization/remap layer.

```yaml
mapping:
  tvgIdRemap:                           # map<string,string>; optional
    "NBC-NewYork": "WNBC"
  channelRename:                        # map<string,string>; optional (by exact channel name)
    "US FOX 5 (WNYW) New York": "FOX 5 New York"
  groupRename:                          # map<string,string>; optional
    "CBS Locals": "Local Networks"
  collapseLocalGroups: false            # bool; if true, merge common local group variants
```

**Behavior**
- Remaps apply **after** filtering by group/title.
- `collapseLocalGroups` is a best-effort heuristic (may be ignored until implemented).

---

## 4) `output`
Where and how to write the final artifacts.

```yaml
output:
  playlistPath: "/data/out/playlist.m3u"   # required
  epgPath: "/data/out/epg.xml"              # required if EPG used
  atomicWrites: true                         # bool, default true (tmp + fsync + rename)
  gzip: false                                # bool; if true, also write .gz alongside
  permissions:
    fileMode: "0644"                         # string, optional (POSIX-style)
    dirMode: "0755"                          # string, optional
  tmpDir: "/data/tmp"                        # string, optional (defaults next to output)
```

**Validation**
- `playlistPath` must be absolute and writable at runtime
- `epgPath` required if `inputs.epg.url` is present
- Safety: writing outside an allowed root may be blocked in Docker

---

## 5) `logging`
Controls verbosity and optional file logging.

```yaml
logging:
  level: "Information"                  # "Debug" | "Information" | "Warning" | "Error"
  file: "/data/logs/iptv-run.log"       # optional; rotate policy TBD
  format: "text"                         # "text" | "json" (CLI may override with --json)
```

---

## Environment Variable Expansion
- Any string field may contain `${NAME}`; values are read from the process environment **after** `--env-file` is loaded (if provided).
- Undefined variables → validation error, unless the entire field is optional and omitted by design.

**Example `.env`**
```
PROVIDER_PLAYLIST_URL=https://example.test/get.php?username=u&password=p&type=m3u_plus&output=ts
PROVIDER_EPG_URL=https://example.test/xmltv.php?username=u&password=p
PLAYLIST_TOKEN=abc123
```

---

## Complete Example (YAML)
```yaml
profiles:
  default:
    inputs:
      playlist:
        url: "${PROVIDER_PLAYLIST_URL}"
        headers:
          User-Agent: "IPTVGuideDog/1.0"
      epg:
        url: "${PROVIDER_EPG_URL}"
        allowCompressed: true

    filters:
      includeGroups: ["USA | News", "USA | Sports"]
      excludeGroups: ["International"]
      excludeTitleRegex: "(?i)\\b(4k|uhd)\\b"
      dropListFile: "/config/drop-groups.txt"

    mapping:
      tvgIdRemap:
        "NBC-NewYork": "WNBC"
      groupRename:
        "CBS Locals": "Local Networks"

    output:
      playlistPath: "/data/out/playlist.m3u"
      epgPath: "/data/out/epg.xml"
      atomicWrites: true
      gzip: false

    logging:
      level: "Information"
      file: "/data/logs/iptv-run.log"
```

---

## JSON Example
```json
{
  "profiles": {
    "default": {
      "inputs": {
        "playlist": {
          "url": "${PROVIDER_PLAYLIST_URL}",
          "headers": { "User-Agent": "IPTVGuideDog/1.0" },
          "timeoutSeconds": 30,
          "retries": 3
        },
        "epg": { "url": "${PROVIDER_EPG_URL}", "allowCompressed": true }
      },
      "filters": {
        "includeGroups": ["USA | News", "USA | Sports"],
        "excludeGroups": ["International"],
        "excludeTitleRegex": "(?i)\\b(4k|uhd)\\b"
      },
      "mapping": {
        "tvgIdRemap": { "NBC-NewYork": "WNBC" },
        "groupRename": { "CBS Locals": "Local Networks" }
      },
      "output": {
        "playlistPath": "/data/out/playlist.m3u",
        "epgPath": "/data/out/epg.xml",
        "atomicWrites": true,
        "gzip": false
      },
      "logging": { "level": "Information" }
    }
  }
}
```

---

## Defaults & Behaviors (Reference)
- `inputs.playlist.timeoutSeconds` = 30; `retries` = 3; `maxDownloadMb` = 200
- `inputs.epg.allowCompressed` = true; `timeoutSeconds` = 60; `retries` = 3; `maxDownloadMb` = 500
- `filters.*` = empty lists/nulls → no filtering
- `mapping.*` absent → no remaps/renames
- `output.atomicWrites` = true; `gzip` = false
- `logging.level` = `Information`; `format` = `text`

---

## Validation & Error Messages
- On invalid or missing required fields, tools must emit a **single, clear line** with the exact key path and reason, e.g.:
  - `config:profiles.default.inputs.playlist.url is required`
  - `config:filters.excludeTitleRegex is invalid regex: unterminated group`

---

## Security Guidance
- Prefer environment variables or secret stores for URLs containing credentials.
- The application must redact values for keys named `Authorization`, `X-Api-Key`, `Password`, or URLs containing `password=` when logging.

---

## Forward Compatibility (Reserved Keys)
- `outputs[]` (plural) – future multi-sink support
- `auth:` section – pluggable credential refs
- `scheduling:` – cron/interval controls for server/daemon modes

Keep existing names stable; add new optional fields rather than renaming.

