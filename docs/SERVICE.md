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

- Ingests one or more provider playlists (M3U) and guide data (XMLTV)
- Normalizes provider channels into **canonical channel identities**
- Lets you define one or more **lineups** (profiles) to publish
- Applies **group inclusion** and **numbering rules**
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
A stable identity representing “the channel” independent of how providers rename/reorder things over time.
Canonical channels are created automatically as new channels are discovered.

### Lineup (Profile)
A named channel lineup published as its own endpoint, for example:

- `/m3u/default`
- `/m3u/livingroom`
- `/m3u/mancave`

A lineup defines:
- which groups/channels are included
- channel numbering behavior
- pinned channel numbers (when you want something exact)

### Snapshot
A published, atomic “version” of a lineup output (playlist + guide mapping + stream keys).
Snapshots allow last-known-good behavior: if a refresh fails, clients keep working.

### Stream Key
A stable identifier used by published stream URLs.
Clients receive a URL like `/stream/<streamKey>` instead of a raw provider URL.

---

## Service Behavior (Predictable by Design)

### Default behavior for new channels
- Newly discovered channels are **not added** to a lineup by default.
- They appear in a **New Channels** view for review.
- Exception: if a lineup has a group enabled for auto-update (see below), channels in that group can be included automatically.

This prevents surprises while still supporting dynamic sports/event groups when you want them.

### Dynamic Groups (auto-update)
In a lineup UI, each group can be enabled with a simple checkbox.

When enabled:
- channels in that group are automatically included
- if the provider adds/removes channels in that group, the lineup updates accordingly

This is intended for groups like:
- weekly sports packages (e.g., `LIVE | NFL (Direct)`)
- rotating event groups (UFC/PPV-style feeds)
- seasonal content

---

## Numbering Rules (tvg-chno)

Channel numbers are **owned by the lineup**, not globally.

Numbering rules are designed to be understandable:

- Each enabled group has a **Start Number** (e.g., NFL starts at 700)
- Channels can also be **pinned** to an explicit number
- Pinned numbers always win
- Groups fill numbers upward starting at the start number, skipping occupied numbers

Example:
- News starts at 100
- Weather is pinned at 105
- News will fill 100–104, skip 105, then continue at 106+

Overflow:
- If a channel cannot be placed after reasonable collision scanning, it is placed in an **Overflow** block at the end and labeled clearly.

See: `NUMBERING_RULES.md`

---

## Snapshot Lifecycle

A refresh run follows this pattern:

1. Fetch provider inputs (M3U + XMLTV)
2. Detect channel/group changes
3. Update canonical channel catalog (auto-create new channels)
4. Apply lineup rules (group enablement + numbering + pinned overrides)
5. Generate a **staged snapshot**
6. Validate snapshot (basic integrity checks)
7. Promote staged snapshot to **active**
8. Serve active snapshot to clients

If refresh fails:
- The service continues serving the last active snapshot (last-known-good).

---

## HTTP Endpoints (Compatibility Layer)

The service publishes endpoints intended to be consumed by IPTV clients and DVR systems.

Typical endpoints:

- `GET /m3u/<profile>`
- `GET /xmltv/<profile>`
- `GET /stream/<streamKey>`
- `GET /images/<id>` (optional, if logos are proxied)

See: `HTTP_COMPATIBILITY.md`

---

## Web UI (Configuration + Visibility)

The Web UI is intended to make large catalogs manageable.

Core views:

- **Providers**: add/edit provider connection details
- **Groups**: see the provider’s groups; enable/disable per lineup
- **Lineups**: manage published lineups (name, cloning, endpoint)
- **Numbering**: start numbers per group; pinned channel numbers
- **New Channels**: review newly discovered channels and decide where they belong
- **Snapshots / Status**: see refresh history and current active snapshot

Design goals:
- configuration should be explicit and understandable
- changes should be visible (what changed, when)
- no surprise renumbering

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
Optional extensions may be introduced in future releases.