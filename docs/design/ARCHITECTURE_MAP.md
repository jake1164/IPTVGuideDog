# Architecture Map

## Purpose
A single container provides:
- Web UI for configuration & status
- HTTP endpoints consumed by IPTV clients:
  - playlist (M3U)
  - guide (XMLTV)
  - proxy streaming (/stream/*)
- background refresh and snapshot publishing

This service is intended to be deployed on a LAN initially; authentication is optional later.

## Component Overview (single container)

Clients (NextPVR, Jellyfin, xTeVe, etc)
        |
        v
+--------------------------------------------------+
| IPTVGuideDog Container                            |
|                                                  |
|  1) Web UI (Blazor)                               |
|     - edits config + rules stored in DB           |
|     - shows refresh status + diagnostics          |
|                                                  |
|  2) Service Host (HTTP)                           |
|     - /m3u/<name>.m3u                             |
|     - /xmltv/<name>.xml                           |
|     - /stream/<streamKey>                         |
|     - /health, /status                             |
|                                                  |
|  3) Background Worker                             |
|     - scheduled refresh                            |
|     - builds snapshot atomically                  |
|     - preserves last-known-good                   |
+--------------------------------------------------+
        |
        v
Persistent Volume
 - config.db (sqlite)
 - snapshots/
    - playlist.m3u
    - epg.xml
    - channelIndex.json
    - status.json
 - logs/

## Key Architectural Rules

### R1 — Stable Channel Identity
- Each channel has a stable canonical ID stored in DB.
- Refresh must not generate new IDs for “the same channel” unless an admin explicitly changes mapping.
- IDs must not be derived from list ordering or volatile provider identifiers.

### R2 — Authoritative Channel Numbering
- The system is the authority for channel numbers (tvg-chno).
- Numbers come from DB rules and remain stable across refreshes.
- Refresh may discover channels, but numbering is assigned deterministically.

### R3 — Snapshot-based Serving (Last-known-good)
- The service always serves an “active snapshot”.
- Refresh produces a new snapshot in a staging area and atomically swaps it to active only when valid.
- If refresh fails, the active snapshot remains unchanged.

### R4 — Proxy Streaming (Simple Relay in V1)
- Playlist stream URLs point to /stream/<streamKey> on this service.
- /stream resolves streamKey -> channel -> chosen source URL.
- V1 behavior is byte relay with timeouts; no ffmpeg required.

## Data Flow

### Refresh Cycle
Provider fetch (playlist + xmltv)
  -> parse
  -> normalize
  -> apply DB rules (filters, grouping, naming, numbering)
  -> resolve/match EPG where possible
  -> build snapshot files
  -> validate snapshot
  -> swap to active

### Request Cycle
- /m3u/*.m3u serves active playlist snapshot
- /xmltv/*.xml serves active xmltv snapshot
- /stream/* proxies from selected source URL defined by the active snapshot
