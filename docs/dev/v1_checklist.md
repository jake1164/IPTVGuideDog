# IPTVGuideDog V1 Checklist
Goal: Minimal end-to-end pass-through service with provider preview UX and a single locked output endpoint.

Client → GuideDog → Provider

Legend: [ ] not started | [~] in progress | [x] done

---

## V1 Scope

- Provider configuration (add/edit multiple providers; one active at a time)
- Snapshot-based M3U + XMLTV output
- Service-owned /stream endpoint
- GUI preview of provider groups/channels
- Single output locked to `/m3u/guidedog.m3u` and `/xmltv/guidedog.xml` (output name is not configurable in Core)
- LAN-only deployment

Lineup shaping, multiple named output endpoints, and redundancy come later.

---

## 1) Persistence (SQLite)

Implement per docs/DB_SCHEMA.md.

Minimum V1 tables:

- [x] providers
- [x] profiles
- [x] profile_providers
- [x] fetch_runs
- [x] provider_groups
- [x] provider_channels
- [x] snapshots (profile-scoped)
- [x] stream_keys (stable per profile+channel)

Optional but schema-present (future use — see DB_SCHEMA.md Appendix C):

- [x] canonical_channels
- [x] channel_sources
- [x] epg_channel_map

Constraints:
- Snapshots scoped to profile_id
- stream_keys NOT tied to snapshot_id
- Last-known-good snapshot preserved

Verification status:
- [x] EF migration generated and applied successfully against SQLite
- [x] Partial unique index for `(provider_id, provider_channel_key)` verified with `WHERE provider_channel_key IS NOT NULL`
- [x] Delete behavior matrix verified via migration SQL and persistence tests

Locked schema decisions (authoritative):
- D1: Enforce `UNIQUE(profile_id, channel_number)` on `canonical_channels`.
- D2: Enforce `provider_channels(provider_id, provider_channel_key)` uniqueness only when key is present with a partial unique index (`provider_channel_key IS NOT NULL`).
- D2 ingest invariant: normalize missing/empty/whitespace `provider_channel_key` to `NULL` before persistence.
- D3 delete behavior policy:
- `providers` -> `fetch_runs`/`provider_groups`/`provider_channels`/`profile_providers`/`channel_sources`: `RESTRICT`
- `profiles` -> `canonical_channels`/`snapshots`/`stream_keys`/`epg_channel_map`: `RESTRICT`
- `profiles` -> `profile_providers`/`channel_match_rules`: `CASCADE`
- `provider_groups` -> `provider_channels.provider_group_id`: `SET NULL`
- `fetch_runs` -> `provider_channels.last_fetch_run_id`: `RESTRICT`
- `canonical_channels` -> `channel_sources`/`stream_keys`/`epg_channel_map`: `CASCADE`
- `provider_channels` -> `channel_sources.provider_channel_id`: `RESTRICT`

---

## 2) Provider Configuration UI

- [x] List/add/edit providers
- [x] Playlist URL + optional EPG URL
- [x] Active toggle (enabled/disabled per provider)
- [x] Associate provider to default profile (V1: single-select)
- [x] Show last refresh status
- [x] Show snapshot timestamp

---

## 3) Provider Preview UX

- [x] Preview groups from latest successful refresh
- [x] Display channel counts per group
- [x] Display first N channels per group (configurable sample size)
- [x] "Refresh & Preview" action (fetches live, upserts DB, returns preview)
- [x] Preview endpoint reusing CLI parser logic (PlaylistParser)
- [x] Read-only preview (no filtering)

---

## 4) Snapshot Fetcher (Hosted Service)

- [x] Scheduled refresh
- [x] On-demand refresh trigger
- [x] Fetch playlist + EPG
- [x] Parse using CLI parser logic
- [x] Populate provider_channels + provider_groups
- [x] Write snapshot files:
      snapshots/{profile}/{snapshotId}/
- [x] Insert snapshot record (staged → active)
- [x] Update fetch_runs
- [x] Preserve last-known-good on failure

---

## 5) Serving Endpoints

- [x] GET /m3u/guidedog.m3u
- [x] GET /xmltv/guidedog.xml
- [x] GET /stream/<streamKey>
- [x] GET /status
- [x] GET /health

Notes:
- Output name is locked to `guidedog` in Core (`/m3u/guidedog.m3u`, `/xmltv/guidedog.xml`)
- /stream implementation is relay-only in V1 (no buffering)
- Playlist must reference /stream/<streamKey> — clients never see raw provider URLs
- Serving must read from the active snapshot, not a live fetch

---

## 6) Wiring UI to API

- [x] Provider CRUD API (`GET`, `POST`, `PUT`, `PATCH /enabled`, `PATCH /active`)
- [x] Status API (`GET /api/v1/providers/{id}/status`)
- [x] Preview endpoint (`GET` + `POST /refresh-preview`)
- [x] Snapshot trigger API (`POST /api/v1/snapshots/refresh` → 202/409)
- [~] Blazor client integration
  - [x] Providers page (CRUD + preview fully wired)
  - [ ] Dashboard rewrite (replace placeholder with real V1 status: active provider, last refresh, active snapshot)

---

## 7) Pre-V1 Cleanup (Stale Scaffolding)

Remove artifacts from the pre-DB socket-host architecture before packaging.

- [x] Remove or replace `ChannelFilters.razor` (V2 group selection — not V1 scope)
- [x] Remove `ChannelWorkspaceState.cs` (in-memory group state, not DB-backed, V2 concept)
- [x] Remove `SocketHostChannelCatalog.cs` (old socket host HTTP client — no longer used)
- [x] Remove `Setup.razor` and "Socket Host" nav item (points to obsolete socket host config)
- [x] Remove `IPTVGuideDog.API` project (old separate-process API; removed from solution and deleted)
- [x] Remove "Channel Filters" from nav menu until V2

---

## 8) Packaging & Ops

- [x] Dockerfile (ASP.NET)
- [x] Volume mounts for DB + snapshots
- [x] Container usage/runbook doc (`docs/dev/container.md`)
- [~] Smoke test (pending execution on host with running Docker engine):
  - [ ] curl /health
  - [ ] curl /m3u/guidedog.m3u
  - [ ] stream via /stream

---

## 9) Tests (Lightweight)

- [ ] Provider validation
- [ ] Snapshot success/failure handling
- [ ] Preview endpoint output
- [ ] Stream timeout behavior

---

# V1 Summary

V1 delivers:

- Single pass-through output: `/m3u/guidedog.m3u` + `/xmltv/guidedog.xml` (no lineup shaping)
- Snapshot-based serving with last-known-good behavior on refresh failure
- Service-owned `/stream/<streamKey>` relay (clients never see raw provider URLs)
- Provider configuration UI with group/channel preview
- LAN-only operation

Lineup shaping, multiple named output endpoints, and redundancy follow in later versions.
