# IPTVGuideDog Web UI

The IPTVGuideDog Web UI is designed to make very large IPTV provider catalogs understandable and manageable.

Many IPTV providers deliver 10,000–50,000+ channels across multiple regions, languages, sports feeds, and temporary event groups. The Web UI exists to give you clear control over what gets published — and why.

The interface emphasizes:

- Explicit configuration  
- Predictable behavior  
- Clear visibility  
- No hidden logic  
- No surprise renumbering  

---

## Core UI Sections

### 1. Providers

The Providers section allows you to configure upstream IPTV sources.

Each provider includes:

- Base URL  
- Credentials  
- Refresh settings  
- Status information  

The UI shows:

- Last refresh time  
- Success/failure status  
- Channel count  
- Group count  

Providers are the raw input layer. They may contain thousands of channels and hundreds of groups.

---

### 2. Groups

The Groups view helps you understand and manage large provider catalogs.

For each provider group, the UI displays:

- Group name  
- Channel count  
- Whether the group is enabled in a lineup  

This is where you tame scale.

Instead of manually filtering thousands of channels, you select the groups you care about.

Example use cases:

- Enable only “USA | News”  
- Enable “LIVE | NFL (Direct)” for seasonal sports  
- Ignore international groups entirely  

Group selection is explicit and visible.

---

### 3. Lineups (Profiles)

A Lineup represents a published channel set with its own endpoint.

Examples:

- `default`  
- `livingroom`  
- `mancave`  

Each lineup controls:

- Which groups are enabled  
- Channel numbering behavior  
- Pinned channel numbers  
- Auto-update behavior for dynamic groups  

Lineups are independent. One lineup can be minimal; another can include sports and events.

The UI makes it clear which lineup you are editing.

---

### 4. Dynamic Groups (Auto-Update)

For each group in a lineup, you can choose:

- Manual inclusion (default)  
- Dynamic (auto-update)  

Manual mode:

- New channels in that group are not automatically added.  
- They appear in “New Channels” for review.  

Dynamic mode:

- Channels added or removed by the provider are automatically reflected in the lineup.  
- Useful for rotating sports feeds or event-based groups.  

This allows you to balance control and convenience.

---

### 5. Numbering

Channel numbering is owned by the lineup.

The Numbering section allows you to:

- Set a Start Number for each enabled group  
- Pin specific channels to exact numbers  
- View conflicts clearly  

Rules are simple and predictable:

- Pinned numbers always win  
- Groups fill upward from their start number  
- Occupied numbers are skipped  
- Overflow channels are placed at the end and clearly labeled  

The UI makes channel position visible and editable.

---

### 6. New Channels

When a provider introduces new channels, they appear here.

By default:

- New channels are not added automatically.  
- You review them before publishing.  

This prevents surprise changes to your lineup.

From this view you can:

- Assign them to a lineup  
- Pin their numbers  
- Ignore them  

This keeps the publishing process transparent.

---

### 7. Snapshots & Status

The Snapshots view shows:

- Last refresh run  
- Active snapshot version  
- Staged snapshot (if pending)  
- Success/failure history  

If a refresh fails, the system continues serving the last active snapshot.

The UI makes this behavior visible so you always know what clients are receiving.

---

### 8. Stream Identity

Each published channel uses a stable stream key.

Clients receive URLs like:

`/stream/<streamKey>`

Stream keys do not change across refreshes unless the canonical channel itself changes.

This protects DVR mappings and client configurations.

---

## UI Design Goals

The Web UI is built around the following principles:

- Explicit over implicit  
- Controlled over automatic  
- Transparent over opaque  
- Scalable for large provider catalogs  
- Self-hosted and privacy-respecting  

Every action in the UI should be understandable without guessing what the system will do next.

---

## Relationship to CLI

The CLI is a file-oriented filtering tool.

The Web UI builds on those same concepts but adds:

- Database-backed configuration  
- Snapshot lifecycle management  
- HTTP endpoint publishing  
- Visual lineup control  

The CLI reduces playlists.  
The GUI manages lineups.

---

## Editions

IPTVGuideDog follows an open-core model.

The current focus is delivering a stable, fully usable self-hosted lineup manager. Optional extensions may be introduced in future releases.
