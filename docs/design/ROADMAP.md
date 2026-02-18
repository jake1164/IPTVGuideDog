# Roadmap

---

## V1 — Stable Catalog + Proxy Streaming + Preview UX

Deliver:

- Provider configuration via GUI
- Profile-scoped outputs:
  - /m3u/<output>.m3u
  - /xmltv/<output>.xml
- Snapshot-based serving with last-known-good preservation
- Service-owned `/stream/<streamKey>` endpoint
- Provider preview UX:
  - Enter provider endpoint
  - Validate connectivity
  - Preview groups and channel counts
  - “Refresh & Preview” action

Notes:

- Channel identity and authoritative numbering are designed into schema.
- If numbering cannot be fully stabilized on day one, pass-through ordering is allowed temporarily.
- LAN-only deployment is acceptable.
- Authentication remains optional in V2.

---

## V2 — Stability & Diagnostics

- Diff view (“what changed from provider”)
- Missing EPG detection
- Duplicate detection
- Basic auth (optional)
- Operational dashboards

---

## V3 — Multi-Provider Mapping + Redundancy

- Canonical channels mapped to multiple provider sources
- Priority + fallback
- Health scoring
- Redundancy switching

---

## V4 — Performance Enhancements

- Optional buffering
- Optional stream relay improvements
- Optional caching
- No breaking changes to playlist contract
