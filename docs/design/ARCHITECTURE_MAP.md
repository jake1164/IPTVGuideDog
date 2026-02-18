# Architecture Map

## Purpose
A single container provides:
- Web UI for configuration & status
- HTTP endpoints consumed by IPTV clients:
  - playlist (M3U)
  - guide (XMLTV)
  - service-owned stream endpoint (/stream/*)
- background refresh and snapshot publishing

## Component Overview (single container)

Clients (NextPVR, Jellyfin, Plex, xTeVe, others)
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
|     - /m3u/<output>.m3u                           |
|     - /xmltv/<output>.xml                         |
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
 - guidedog.db (sqlite: config/state)
 - snapshots/
    - playlist.m3u
    - epg.xml
    - channelIndex.json
    - status.json
 - logs/

## Key Architectural Rules

### R1 — Stable Channel Identity
- Each published channel is represented by a canonical channel ID stored in the DB.
- Refresh must not generate new IDs for “the same channel” unless an admin explicitly remaps it.
- IDs must not be derived from list ordering or volatile provider identifiers.

### R2 — Authoritative Channel Numbering
- The system is the authority for channel numbers (tvg-chno).
- Numbers come from DB rules and remain stable across refreshes.
- Output ordering is primarily by channel number.

### R3 — Snapshot-based Serving (Last-known-good)
- The service always serves an “active snapshot” (playlist+xmltv+index+status).
- Refresh produces a new snapshot in staging and swaps it to active only after validation.
- If refresh fails, the active snapshot remains unchanged.

### R4 — Service-owned /stream
- Playlist stream URLs must be owned by this service:
  - /stream/<streamKey>
- /stream resolves streamKey -> canonical channel -> selected source.
- The specific implementation (redirect vs relay) is an internal detail and may evolve without changing the playlist contract.

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
- /stream/* serves the playable stream for the channel defined by the active snapshot
