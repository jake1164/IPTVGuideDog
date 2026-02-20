# Roadmap

## V1 — Passthrough + Stability
- Provider configuration (URL, credentials, refresh settings)
- Active provider selection (single active provider at a time)
- Provider group preview (read-only catalog browse)
- Snapshot-based serving (staged → active lifecycle)
- Last-known-good behavior on refresh failure
- M3U + XMLTV compatibility endpoints (`/m3u/guidedog.m3u`, `/xmltv/guidedog.xml`)
- Stream proxy relay (`/stream/<streamKey>`, relay-only, no buffering)
- Stable stream keys (provider switch regenerates; warning shown to user)

## V2 — Lineup Shaping
- Group inclusion rules (select which groups appear in your lineup)
- Channel numbering (start ranges, pinned numbers, overflow handling)
- New channels inbox (review and approve newly discovered channels before publishing)
- Dynamic groups (auto add/drop for rotating sports or event feeds)
- Provider switch assistance (diff view + optional manual channel mapping hints)
- HDHomeRun (HDHR) device emulation (`/discover.json`, `/lineup.json`, `/lineup_status.json`) — allows Plex, Emby, and Jellyfin to auto-discover IPTVGuideDog as a network tuner without manual M3U configuration. Requires stable channel numbering from lineup shaping.

## Future
- Auth configuration: ASP.NET Core Identity infrastructure is present. First-run wizard will configure whether UI auth is required. Compatibility endpoints remain unauthenticated for IPTV client access.
- Change history / diff view
- Additional diagnostic and operational tooling
