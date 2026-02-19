# IPTVGuideDog 🐕

A self-hosted IPTV lineup manager built for large provider catalogs.

IPTVGuideDog helps you take control of massive IPTV playlists and publish clean, predictable lineups for DVR and media server environments.

Designed for self-hosted systems like NextPVR, Jellyfin, or any client that consumes M3U + XMLTV.

---

## Why IPTVGuideDog Exists

Modern IPTV providers often deliver enormous catalogs — 10,000 to 50,000+ channels across multiple regions, languages, sports packages, and temporary event feeds.

Most users only need a small, carefully selected subset of those channels.

Managing that scale can be difficult:

- Massive group lists with mixed languages
- Constantly rotating sports or event feeds
- Temporary PPV channels
- Duplicate regional variations
- Unclear mapping between configuration and published output
- Hard-to-understand numbering behavior

IPTVGuideDog is designed to make large IPTV catalogs manageable.

It focuses on:

- Clear group selection
- Explicit inclusion rules
- Controlled channel numbering
- Stable channel identity
- Transparent publishing
- Predictable refresh behavior

The goal is simple:

Give you control over what gets published — and make it understandable.

---

## What IPTVGuideDog Is

IPTVGuideDog is a lineup management system for IPTV providers.

It:

- Connects to your IPTV provider
- Normalizes channels into canonical identities
- Allows you to define a controlled lineup
- Preserves stream key stability
- Protects DVR mappings from churn
- Publishes compatibility endpoints expected by IPTV clients

It is not just a playlist filter.
It is a system for managing IPTV catalogs at scale.

---

## Components

### CLI (Available Now)

The CLI was the first component and remains useful for automation and scripting.

It supports:

- Provider playlist fetching
- Group discovery
- M3U filtering
- XMLTV filtering
- Secure `.env` credential handling

See: `docs/CLI.md`

---

### Service + Web UI (In Development)

The service layer introduces:

- Database-backed configuration
- Group-based inclusion rules
- Stable numbering rules
- Snapshot lifecycle management
- HTTP compatibility endpoints
- Stream relay proxy

See: `docs/SERVICE.md`

---

## Compatibility Endpoints

IPTVGuideDog publishes endpoints compatible with common IPTV clients:

- `/m3u/guidedog.m3u`
- `/xmltv/guidedog.xml`
- `/stream/<streamKey>`

See: `docs/design/HTTP_COMPATIBILITY.md`

---

## Design Principles

- Explicit over implicit
- Controlled over automatic
- Transparent over opaque
- Scalable for large provider catalogs
- Self-hosted and privacy-respecting

---

## Editions

IPTVGuideDog follows an open-core model.

The current focus is delivering a stable, fully usable self-hosted lineup manager. Advanced features may be introduced in future releases.

---

## License

Core: Apache License 2.0
See `LICENSE` for details.

---

## Status

The CLI is stable and usable today.
The service layer is under active development with an architecture-first approach.
