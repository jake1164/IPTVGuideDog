# IPTVGuideDog Plan (Configuration UI + Compatibility Service)

## Product Goal
A self-hosted IPTV lineup manager that:
- connects to an IPTV provider
- publishes client-friendly endpoints (M3U, XMLTV, stream proxy)
- prevents DVR churn by keeping stable channel identity and stable numbering
- gives users clear control over what gets published

Published as:
- /m3u/guidedog.m3u
- /xmltv/guidedog.xml

## V1 Definition
V1 delivers:
- Provider configuration via GUI
- Active provider selection (single active provider; warning shown on switch)
- Provider group preview (read-only catalog browse)
- Snapshot-based serving (staged → active lifecycle)
- Last-known-good behavior on refresh failure
- Stable stream keys per channel (regenerate on provider switch; warning shown)
- Stream proxy is relay-only

No lineup shaping in V1. No buffering/caching in V1. Canonical channel identity is a future feature.

## V2 Definition
V2 adds full lineup shaping for the active provider:
- Group inclusion rules
- Channel numbering (start numbers, pinned numbers, overflow)
- New channels inbox (review before publishing)
- Dynamic groups (auto-update for rotating sports/event feeds)
- Provider switch assistance (diff + optional manual mapping hints)

## Documents
- docs/design/ARCHITECTURE_MAP.md
- docs/design/DB_SCHEMA.md
- docs/design/HTTP_COMPATIBILITY.md
- docs/design/LINEUP_RULES.md
- docs/design/NUMBERING_RULES.md
- docs/dev/ROADMAP.md
- docs/dev/v1_checklist.md
- docs/dev/V1_SECTIONS_2_3_ENDPOINT_CONTRACTS.md
- docs/dev/DESIGN_DECISIONS.md
