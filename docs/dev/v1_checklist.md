# IPTVGuideDog V1 Checklist
Goal: Minimal end-to-end pass-through service with provider preview UX and profile-scoped outputs.

Client → GuideDog → Provider

Legend: [ ] not started | [~] in progress | [x] done

---

## V1 Scope

- Provider configuration
- Snapshot-based playlist + xmltv
- Service-owned /stream endpoint
- GUI preview of provider groups/channels
- Profile-scoped outputs
- LAN-only deployment

Filtering and redundancy come later.

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

Optional but schema-present:

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

- [ ] List/add/edit providers
- [ ] Playlist URL + optional EPG URL
- [ ] Active toggle
- [ ] Associate provider to default profile
- [ ] Show last refresh status
- [ ] Show snapshot timestamp

---

## 3) Provider Preview UX (New V1 Scope)

- [ ] Preview groups from latest snapshot
- [ ] Display channel counts
- [ ] Display first N channels
- [ ] “Refresh & Preview” action
- [ ] Preview endpoint reusing CLI parser logic
- [ ] Read-only preview (no filtering yet)

---

## 4) Snapshot Fetcher (Hosted Service)

- [ ] Scheduled refresh
- [ ] On-demand refresh
- [ ] Fetch playlist + epg
- [ ] Parse using CLI logic
- [ ] Populate provider_channels + provider_groups
- [ ] Write snapshot files:
  snapshots/{profile}/{snapshotId}/
- [ ] Insert snapshot record (staged → active)
- [ ] Update fetch_runs
- [ ] Preserve last-known-good on failure

---

## 5) Serving Endpoints

- [ ] GET /m3u/guidedog.m3u
- [ ] GET /xmltv/guidedog.xml
- [ ] GET /stream/<streamKey>
- [ ] GET /status
- [ ] GET /health

Notes:
- Core locks output name to `guidedog` (i.e., `/m3u/guidedog.m3u`, `/xmltv/guidedog.xml`)
- /stream implementation internal (relay for V1)
- Playlist must reference /stream/<streamKey>
- Snapshot must be used for serving

---

## 6) Wiring UI to API

- [ ] Provider CRUD API
- [ ] Status API
- [ ] Preview endpoint
- [ ] Blazor client integration

---

## 7) Packaging & Ops

- [ ] Dockerfile (ASP.NET)
- [ ] Volume mounts for DB + snapshots
- [ ] Smoke test:
  - curl /health
  - curl /m3u/guidedog.m3u
  - stream via /stream

---

## 8) Tests (Lightweight)

- [ ] Provider validation
- [ ] Snapshot success/failure handling
- [ ] Preview endpoint output
- [ ] Stream timeout behavior

---

# V1 Summary

V1 delivers:

- Profile-scoped pass-through outputs
- Snapshot-based serving
- Service-owned stream endpoint
- Provider preview UX
- LAN-only operation

Filtering, canonical UI, and redundancy follow.
