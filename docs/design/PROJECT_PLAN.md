# IPTVGuideDog Project Plan

## Current Direction

V1 is a **profile-scoped pass-through compatibility service** with a provider preview UX.

The system will:

- Allow configuration of providers via GUI
- Fetch provider playlist + EPG into snapshots
- Serve `/m3u/<output>.m3u` and `/xmltv/<output>.xml`
- Serve a service-owned `/stream/<streamKey>` endpoint
- Provide a preview of groups/channels in the Web UI
- Preserve last-known-good snapshots on failure

Filtering, canonical mapping UI, multi-provider redundancy, and advanced stream logic follow in later versions.

---

## V1 Objective: Stable Compatibility Service + Preview UX

Deliver a single Docker container that:

1. Stores provider configuration in SQLite
2. Uses profile-scoped outputs (`/m3u/<output>.m3u`)
3. Fetches and snapshots provider playlists
4. Serves snapshot-based playlist + xmltv
5. Serves `/stream/<streamKey>` for playback
6. Provides GUI preview of provider groups + channels
7. Preserves last-known-good output on failure

---

## V1 Acceptance Criteria

- Playlist header includes url-tvg/x-tvg-url referencing this service
- Playlist stream URLs use `/stream/<streamKey>`
- Snapshot lifecycle supports staged → active → failed
- Refresh failure does NOT churn lineup
- GUI shows:
  - provider configuration
  - last refresh status
  - preview of groups + channel counts
- LAN-only acceptable for V1

---

## Non-Goals (V1)

- Filtering rules UI
- Multi-provider redundancy
- Canonical mapping UI (schema may exist but not required in UI)
- Stream buffering/transcoding
- External authentication (LAN-only)
