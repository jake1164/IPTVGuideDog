# IPTVGuideDog Plan (Configuration UI + Compatibility Service)

## Product Goal
A self-hosted IPTV lineup manager that:
- aggregates channels from many providers
- publishes client-friendly endpoints (M3U, XMLTV, stream proxy)
- prevents DVR churn by keeping stable channel identity and stable numbering
- lets users create multiple “lineups” (profiles) for different devices/rooms

Examples:
- /m3u/default
- /m3u/livingroom
- /m3u/mancave

## V1 Definition
V1 delivers:
- DB-backed configuration via GUI (rules live in DB)
- per-lineup group inclusion (checkbox)
- per-lineup group numbering (start number)
- stable canonical channel identity
- stable per-lineup stream keys
- snapshot + last-known-good serving
- stream proxy is relay-only

No buffering/caching in V1.

## Documents
- docs/ARCHITECTURE_MAP.md
- docs/LINEUP_RULES.md
- docs/NUMBERING_RULES.md
- docs/ROADMAP.md
