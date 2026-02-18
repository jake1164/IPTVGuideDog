# Roadmap

## V1 — Stability + Compatibility
- DB-backed GUI configuration
- Lineups (profiles) as separate endpoints
- Dynamic Groups (checkbox include + auto-update)
- Per-lineup numbering (start number) + pinned numbers
- New channels inbox
- Stable canonical identity
- Stable per-lineup stream keys
- Snapshot + last-known-good serving
- Stream proxy relay (no buffering)

## V2 — Security + Diagnostics
- Optional basic auth for UI and/or endpoints
- “What changed and why” diff view
- Better new-channel categorization and dedupe suggestions

## V3 — Multi-source redundancy (failover)
- Multiple sources per canonical channel (many providers)
- Per-lineup preferred source order
- Failover in stream proxy on errors

## V4 — Buffering / caching
- Optional buffering/caching to improve playback stability
