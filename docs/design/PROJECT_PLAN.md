# IPTVGuideDog Project Plan (Compatibility Service + UI)

## Current State
- CLI is operational for fetching playlists/EPG and filtering channels.
- Next phase is a containerized “service host” that serves playlist+xmltv+stream proxy endpoints and a GUI-backed configuration store.

## V1 Objective (Minimum Viable Compatibility)
Deliver a single Docker container that:
1) serves playlist and xmltv with stable channel IDs and stable numbering
2) serves proxy stream URLs used by clients (/stream/*)
3) refreshes on a schedule and never churns the lineup when providers break
4) stores rules in a DB configured via GUI

## Documents
- docs/ARCHITECTURE_MAP.md — components, boundaries, data flow, storage
- docs/HTTP_COMPATIBILITY.md — endpoints and semantics
- docs/ROADMAP.md — incremental upgrades (failover, buffering, auth)

## V1 Acceptance Criteria
- Playlist header includes url-tvg/x-tvg-url pointing to this service’s XMLTV endpoint.
- Playlist uses authoritative tvg-chno numbering controlled by DB rules.
- Playlist stream URLs point to this service (/stream/<streamKey>), not upstream providers.
- Refresh failure does NOT change playlist order/IDs/numbering; last-known-good remains served.
- Refresh success produces stable results (no renumber/reorder unless rules changed).
- Simple relay stream proxy works for NextPVR/Jellyfin (no ffmpeg required in V1).
