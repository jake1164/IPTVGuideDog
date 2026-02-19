# Architecture Map

## Purpose
A single container provides:
- Web UI (configuration + status)
- HTTP endpoints for clients:
  - M3U (playlist)
  - XMLTV (guide)
  - Stream proxy (/stream/*)
- Background refresh that builds snapshots and serves last-known-good

## Core Concepts
- Provider: upstream source of channels (single active provider in Core)
- Canonical Channel: stable identity representing a channel concept
- Lineup (Profile): user-facing channel list exposed via its own endpoint
- StreamKey: stable forever per (lineup, canonical channel)
- Snapshot: atomic published output set for a lineup

## Key V1 Requirements
- Stable identity: canonical channels must not churn on refresh.
- Authoritative numbering: tvg-chno is owned by the lineup rules.
- Last-known-good snapshots: refresh failures do not break clients.
- Stream proxy required: playlists point to /stream/* URLs (relay-only in V1).

## V1 Client Contract
- Playlist includes url-tvg pointing at this service's XMLTV endpoint.
- Playlist stream URLs point to this service's /stream/* endpoint.
- Clients do not consume raw provider URLs.
