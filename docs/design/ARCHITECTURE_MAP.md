# Architecture Map

## Purpose
A single unified process (`IPTVGuideDog.Web`) provides:
- Web UI (configuration + status) — Blazor Server
- REST API for UI communication (`/api/v1/*`)
- HTTP compatibility endpoints for IPTV clients:
  - M3U — `/m3u/guidedog.m3u` (output name locked in Core)
  - XMLTV — `/xmltv/guidedog.xml`
  - Stream proxy — `/stream/<streamKey>`
- Background refresh service that builds snapshots and serves last-known-good

Note: The `IPTVGuideDog.API` project is a pre-DB artifact (socket-host architecture) and is being removed. All serving and API endpoints live in the Web project.

## Core Concepts
- Provider: upstream source of channels. Multiple providers can be configured and browsed; one is active at a time.
- Canonical Channel: stable identity representing a channel concept, independent of provider churn. Forms the basis for lineup shaping in a future release.
- Profile: scopes a set of providers, snapshots, and stream keys to a named output. Currently a single default profile. Multiple profiles with named output endpoints are a future feature.
- StreamKey: stable token used in published `/stream/<streamKey>` URLs. Stable per (profile, canonical channel) so DVR recordings survive provider URL changes.
- Snapshot: atomic published output for a profile (M3U + XMLTV + stream key index). Staged then promoted to active.

## Key V1 Requirements
- Single active provider: only one provider drives the published output at a time.
- Output name locked: Core publishes to `/m3u/guidedog.m3u` and `/xmltv/guidedog.xml`. Named per-profile endpoints are a future feature.
- Last-known-good snapshots: refresh failures do not break clients. The last active snapshot continues to be served.
- Stream proxy required: published playlists reference `/stream/<streamKey>` — clients never see raw provider URLs.
- Pass-through: no group filtering, no channel numbering, no lineup shaping. All provider channels are published as-is.

## V1 Client Contract
- Playlist includes `url-tvg` pointing at this service's `/xmltv/guidedog.xml` endpoint.
- All stream URLs in the playlist point to this service's `/stream/<streamKey>` endpoint.
- Clients do not consume raw provider URLs.
- The output endpoint is always `/m3u/guidedog.m3u` — clients should be pointed here.
