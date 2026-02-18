# Roadmap

## V1 — Stable Catalog + Proxy Streaming
- DB-backed rules configured via GUI
- Authoritative numbering
- Stable channel identity
- Snapshot-based serving + last-known-good
- /stream proxy is relay-only (no ffmpeg)

## V2 — Diagnostics + Optional Auth
- UI:
  - “what changed and why” diff view
  - missing EPG / duplicate channel reports
- Optional Basic Auth toggle for UI and/or endpoints

## V3 — Provider Redundancy (Per-channel Failover)
Goal: CBS(A) with CBS(B) fallback, etc.
- DB stores per-channel source preference:
  - primary provider source
  - fallback sources ordered
- /stream detects stream failures and switches sources
- status exposes per-channel/provider health signals

## V4 — Buffering / Caching
- optional buffering/caching layer (ffmpeg or segment cache)
- target: reduce client buffering, improve stream stability
