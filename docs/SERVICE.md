# IPTVGuideDog Service + Web UI

The IPTVGuideDog service is the self-hosted component that turns large IPTV provider catalogs into clean, controlled, DVR-friendly lineups.

It is designed for the common real-world problem:

- Providers deliver 10,000–50,000+ channels
- Most are irrelevant (wrong language/region, duplicates, temporary events)
- Configuration in many tools is difficult to understand and hard to maintain
- Users need explicit control over what is published and why

The service focuses on **clarity, control, and predictable behavior**.

---

## What the Service Does

At a high level, the service:

- Ingests a provider playlist (M3U) and guide data (XMLTV)
- Normalizes provider channels into **canonical channel identities**
- Builds **snapshots** and serves **last-known-good** output
- Publishes compatibility endpoints for clients:
  - M3U
  - XMLTV
  - Stream relay proxy

---

## Core Concepts (User-Facing)

### Provider
An upstream IPTV source you configure (URL + credentials). Providers can be large and noisy.

### Group
A category label from the provider playlist (e.g., `USA | News`, `LIVE | NFL (Direct)`, `UFC Fight Night | ...`). Groups are the primary way users tame large catalogs.

### Canonical Channel
A stable identity representing "the channel" independent of how providers rename/reorder things over time.
Canonical channels are created automatically as new channels are discovered.

### Lineup
The published channel set, served at the well-known endpoint:

- `/m3u/guidedog.m3u`
- `/xmltv/guidedog.xml`

### Snapshot
A published, atomic "version" of a lineup output (playlist + guide mapping + stream keys).
Snapshots allow last-known-good behavior: if a refresh fails, clients keep working.

### Stream Key
A stable identifier used by published stream URLs.
Clients receive a URL like `/stream/<streamKey>` instead of a raw provider URL.

---

## Snapshot Lifecycle

A refresh run follows this pattern:

1. Fetch provider inputs (M3U + XMLTV)
2. Detect channel/group changes
3. Update canonical channel catalog (auto-create new channels)
4. Generate a **staged snapshot**
5. Validate snapshot (basic integrity checks)
6. Promote staged snapshot to **active**
7. Serve active snapshot to clients

If refresh fails:
- The service continues serving the last active snapshot (last-known-good).

---

## HTTP Endpoints (Compatibility Layer)

The service publishes endpoints intended to be consumed by IPTV clients and DVR systems.

- `GET /m3u/guidedog.m3u`
- `GET /xmltv/guidedog.xml`
- `GET /stream/<streamKey>`

See: `docs/design/HTTP_COMPATIBILITY.md`

---

## Web UI (Configuration + Visibility)

The Web UI is intended to make large catalogs manageable.

V1 views:

- **Provider**: configure your IPTV provider connection details
- **Groups**: browse the provider's groups and channel counts (read-only preview)
- **Snapshots / Status**: see refresh history and the current active snapshot

Design goals:
- configuration should be explicit and understandable
- changes should be visible (what changed, when)
- no surprise renumbering

---

## Lineup Shaping (Planned — V2)

A future release will introduce full lineup shaping for your active provider:

- Group inclusion rules (select which groups appear in your lineup)
- Channel numbering (start ranges, pinned numbers, overflow handling)
- New channels inbox (review and approve newly discovered channels)
- Dynamic groups for rotating sports or event feeds

These features are planned for V2 and are not part of the initial release.

---

## Relationship to the CLI

The CLI is a file-oriented tool for filtering large playlists.
The service builds on the same core ideas but adds:

- DB-backed configuration
- snapshot publishing
- HTTP endpoints
- web-based management and visibility

See: `CLI.md`

---

## Editions Note

IPTVGuideDog follows an open-core model.
The current focus is delivering a stable, fully usable self-hosted lineup manager.
Advanced features may be introduced in future releases.
