# HTTP Compatibility

## Goals
- IPTV clients can consume M3U + XMLTV + stream URLs from this service reliably.
- The service controls channel identity and numbering to avoid DVR/guide churn.
- The playlist and stream URL contract is stable even if internal implementation changes.
- Provider credentials are never exposed to clients. Stream relay is a security requirement.

## Endpoint Naming

The service uses lineup-scoped endpoint paths. In Core, the lineup name is fixed to `guidedog`:

- `/m3u/guidedog.m3u`
- `/xmltv/guidedog.xml`
- `/stream/<streamKey>`

## Endpoints

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
- GET /m3u/guidedog.m3u
  - MUST include:
    - #EXTM3U url-tvg="http(s)://<host>/xmltv/guidedog.xml" x-tvg-url="..."
  - Each channel entry SHOULD include:
    - tvg-chno (from provider, if present)
    - tvg-name (canonical display name)
    - tvg-id (stable ID or mapped xmltv id)
    - tvg-logo (canonical or provider)
    - group-title (canonical group)
  - Each channel entry MUST point stream URL to:
    - http(s)://<host>/stream/<streamKey>

### Guide (XMLTV)
- GET /xmltv/guidedog.xml
  - XMLTV aligned with published channels
  - Channel ids should be stable over time for canonical channels

### Stream
- GET /stream/<streamKey>
  - Resolves streamKey -> canonical channel in active snapshot
  - Serves playable stream for that channel
  - Must be resilient:
    - no service crashes due to upstream failures
    - clear HTTP failure for that request if upstream fails
  - **MUST relay the stream — MUST NOT redirect to the upstream provider URL.**
    Provider stream URLs typically embed credentials (`http://provider/{username}/{password}/stream.ts`).
    An HTTP 302 redirect would deliver raw credentials to every client that follows the stream URL.
    Relay is a security contract, not an implementation detail. This MUST NOT be changed to a redirect.

## Authentication
Auth infrastructure (ASP.NET Core Identity) is present in the codebase. Whether to enable it is configured at first-run setup. Compatibility endpoints (`/m3u/`, `/xmltv/`, `/stream/`) are designed to be accessible without auth to support LAN IPTV clients. The web UI can optionally require login.
