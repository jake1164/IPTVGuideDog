# HTTP Compatibility

## Goals
- IPTV clients can consume M3U + XMLTV + stream URLs from this service reliably.
- The service controls channel identity and numbering to avoid DVR/guide churn.
- The playlist and stream URL contract is stable even if internal implementation changes.

## V1 Endpoints (Required)

### Health
- GET /health
  - 200 if process is up
  - body may be minimal

### Status (machine-readable)
- GET /status
  - JSON including:
    - active snapshot id/timestamp
    - last refresh result (ok/fail)
    - counts (channels seen, channels published)
    - provider statuses (ok/fail + last error summary)
    - degraded flags

### Playlist (M3U)
- GET /m3u/<output>.m3u
  - MUST include:
    - #EXTM3U url-tvg="http(s)://<host>/xmltv/<output>.xml" x-tvg-url="..."
  - Each channel entry SHOULD include:
    - tvg-chno (authoritative)
    - tvg-name (canonical display name)
    - tvg-id (stable ID or mapped xmltv id)
    - tvg-logo (canonical or provider)
    - group-title (canonical group)
  - Each channel entry MUST point stream URL to:
    - http(s)://<host>/stream/<streamKey>

### Guide (XMLTV)
- GET /xmltv/<output>.xml
  - XMLTV aligned with published channels
  - Channel ids should be stable over time for canonical channels

### Stream
- GET /stream/<streamKey>
  - Resolves streamKey -> canonical channel in active snapshot
  - Serves playable stream for that channel
  - Must be resilient:
    - no service crashes due to upstream failures
    - clear HTTP failure for that request if upstream fails
  - Implementation details (redirect vs relay) are internal and may change.

## Authentication
V1 assumes LAN-only is acceptable. Auth for UI/endpoints can be added later.
