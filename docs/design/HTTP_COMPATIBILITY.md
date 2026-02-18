# HTTP Compatibility

## V1 Endpoints (Required)

### Health
- GET /health
  - 200 if process is up
  - body may be minimal

### Status (machine-readable)
- GET /status
  - returns JSON with:
    - active snapshot id / timestamp
    - last refresh result (ok/fail)
    - counts (channels total, channels published)
    - provider status (ok/fail)
    - last error summary

### Playlist (M3U)
- GET /m3u/<name>.m3u
  - returns M3U
  - MUST include:
    - #EXTM3U url-tvg="http(s)://<host>/xmltv/<name>.xml" x-tvg-url="..."
  - Each channel entry MUST include:
    - tvg-chno (authoritative numbering)
    - stable channel identifier fields (tvg-id or custom attribute)
    - stream URL pointing to:
      - http(s)://<host>/stream/<streamKey>

### Guide (XMLTV)
- GET /xmltv/<name>.xml
  - returns XMLTV aligned with published channels

### Stream Proxy
- GET /stream/<streamKey>
  - resolves streamKey -> channel in active snapshot
  - proxies bytes from chosen upstream URL (simple relay)
  - MUST:
    - apply timeouts
    - not crash the service on upstream failures
    - return clear HTTP failure for that request when upstream is down

## Notes on Authentication
V1 assumes LAN-only and does not require auth.
V2 may add optional Basic Auth for UI and/or endpoints.
