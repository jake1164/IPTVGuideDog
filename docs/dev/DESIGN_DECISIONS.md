# Design Decisions

Decisions made during architecture review. These lock expected behavior for implementation.

---

## 1. Single Endpoint Enforcement (Open-Core Boundary)

**Decision:** Single-endpoint constraint is enforced by code, not by DB schema. Pro features live in closed-source assemblies never shipped with Core.

**Rationale:** A DB flag or config check can be bypassed by anyone with DB access or who forks the repo. The only unbypassable boundary is code that literally does not exist in the Core binary.

**Implementation approach:**
- Core hardcodes `output_name = "guidedog"` in the serving layer — no runtime override path.
- Pro output name configuration is implemented in a separate closed-source assembly (`IPTVGuideDog.Pro`) loaded at startup if present and licensed.
- Core never calls Pro code; Pro injects itself via a well-defined extension point (e.g., `IOutputNameProvider` interface resolved from DI — Core provides the default, Pro overrides it).
- License validation in Pro is performed at startup and periodically (monthly check against Anthropic-hosted endpoint). On validation failure Pro degrades: removes its DI registrations, returns to Core defaults. No offline perpetual unlock.
- DB fields (`profiles.output_name`, `profiles.merge_mode`, etc.) are present in Core schema but the Core serving layer ignores them (reads literal "guidedog"). Pro populates and reads them.
- Users can edit the DB directly — they will find the fields, but Core code won't read them. No exploit surface.

**Result:** Forking Core and removing the hardcoded path check still won't produce a working multi-endpoint build — the Pro logic for routing, naming, and profile management doesn't exist in Core at all.

---

## 2. Separate API Process (Security Boundary)

**Decision:** Do NOT use a separate API process. Use authorization policies within the single `IPTVGuideDog.Web` process.

**Rationale:** A second process adds deployment complexity (two services to configure, run, and keep in sync) without meaningful security benefit for this use case. The security requirement is:
- Admin UI + `/api/v1/*`: optionally require login
- Compatibility endpoints (`/m3u/`, `/xmltv/`, `/stream/`, `/health`, `/status`): always unauthenticated (LAN IPTV clients cannot handle auth)

ASP.NET Core handles this cleanly in a single process via per-endpoint authorization policies:
- Admin endpoints: `.RequireAuthorization()` (when auth is enabled)
- Compatibility endpoints: `.AllowAnonymous()` (always, explicitly)

The dead `IPTVGuideDog.API` project (socket-host era) is being removed. No repurposing needed.

**Auth enforcement note:** `UseHttpsRedirection()` must be removed or moved behind an environment check. LAN IPTV clients send HTTP requests; a 301 redirect breaks them silently. HTTPS can be offered as an option for remote access but must not be mandatory for LAN operation.

---

## 3. Optional Auth — First-Run Wizard

**Decision:** Auth is configured once at first run via a setup wizard. The decision is stored durably and cannot be changed at runtime through the UI.

**First-run detection:** Check whether any users exist in the Identity tables on startup. If none: redirect all requests to `/setup` until setup is complete.

**Wizard flow:**
1. `/setup` is always anonymous (accessible before auth exists).
2. User chooses: "Require login for admin UI? Yes / No"
3. If Yes: user creates the first admin account (username + password). Email confirmation is disabled (no email sender configured) — account is confirmed immediately.
4. A `settings` record is written: `auth_mode = "required"` or `"disabled"`.
5. A marker record is written: `setup_complete = true`.
6. `/setup` middleware: if `setup_complete = true`, redirect to home. Setup cannot be re-entered.

**Making it irreversible at runtime:**
- No UI setting to change auth mode once set.
- Changing auth mode requires: stop the service, set `auth_mode` in `appsettings.json` (or DB via CLI tool), restart.
- The CLI tool (`iptvguidedog admin auth`) can reset auth mode with explicit flags — this is a privileged operation requiring shell access to the host, which is already a trusted boundary.

**RequireConfirmedAccount fix:**
- Change `options.SignIn.RequireConfirmedAccount = false` for Core.
- No email confirmation flow is needed when the admin creates their own account at first run.

---

## 4. Comparison: Threadfin / xTeVe

Both are Go applications, single process, port 34400 by default. Both assume trusted LAN, fully open-source, no subscription enforcement.

| Feature | Threadfin | xTeVe | IPTVGuideDog |
|---|---|---|---|
| Language | Go | Go | C# / ASP.NET Core |
| Process model | Single | Single | Single (Web project) |
| UI protocol | WebSocket (`/data/`) | WebSocket | Blazor Server (SSR + circuit) |
| Auth | Optional; `AuthenticationWEB` flag; token + cookie | Optional (similar) | Optional; first-run wizard; ASP.NET Identity |
| Auth scope | Web UI + API independently | Web UI + API independently | Web UI + `/api/v1/*`; compatibility endpoints always anonymous |
| M3U endpoint | `/m3u/` | `/m3u/` | `/m3u/guidedog.m3u` |
| XMLTV endpoint | `/xmltv/` | `/xmltv/` | `/xmltv/guidedog.xml` |
| Stream proxy | Relay (default) OR redirect (configurable) | Relay with buffer | Relay only — never redirect |
| Multiple upstream sources | Yes, merged simultaneously | Yes, merged simultaneously | Yes, one active at a time (intentional design) |
| Subscription enforcement | None | None | Open-core: Pro features in separate closed-source assembly |
| HDHR emulation | Yes (Plex/Emby discovery) | Yes | Not planned for V1 |

**Key decisions informed by this comparison:**

1. **Relay-only stream proxy is correct.** Threadfin's redirect mode exposes provider credentials embedded in URLs directly to clients. Our relay-only policy prevents this. Not making redirect configurable is the right security call.

2. **Single-active-provider model is intentional differentiation.** Threadfin and xTeVe merge all sources simultaneously — this is powerful but creates channel ID churn when upstream changes. IPTVGuideDog's one-active model gives stable identity at the cost of flexibility. This is the correct tradeoff for DVR stability.

3. **Auth scope split is the right call.** Both Threadfin and xTeVe have separate auth flags for web vs API vs PMS. We should explicitly mark compatibility endpoints as `[AllowAnonymous]` regardless of whether UI auth is enabled.

4. **Port selection:** Avoid 34400 (used by both Threadfin and xTeVe). Default to 5000/5001 (ASP.NET Core defaults) or let users configure. Document that running alongside Threadfin/xTeVe requires different ports.

5. **HDHR emulation:** Both competitors support HDHR device discovery (`discover.json`, `lineup_status.json`). This is valuable for Plex/Emby integration. Consider for a future release — not V1.

---

## 5. Provider DELETE Endpoint

**Decision:** Allow provider delete with dependency check + explicit confirmation. Soft-delete is not needed.

**Rules:**
- **Cannot delete the active provider.** Return 409 with message "Deactivate the provider before deleting it."
- **Snapshots are owned by the profile, not the provider.** Snapshots survive provider deletion (they are the last-known-good data).
- **FK cleanup on delete:** `profile_providers` rows for this provider are cascade-deleted. `channel_sources` for this provider are cascade-deleted (canonical channel mappings lose their source, which is acceptable — canonical channels themselves are not deleted).
- **UI:** Delete button shows only for non-active providers. Confirmation dialog lists what will be removed.

**API endpoint:** `DELETE /api/v1/providers/{id}`
- 409 if provider is active
- 204 on success

---

## 6. Snapshot Retention Policy

**Decision:** Keep active snapshot + 2 previous by default. Auto-purge after each successful promotion.

**Policy:**
- Minimum retained: 1 (active only)
- Default retained: 3 (active + 2 previous)
- Maximum: configurable, default unlimited option available
- Purge trigger: immediately after a new snapshot is promoted to active
- Purge order: oldest first, by `created_at`
- Active snapshot is never purged

**Storage:**
- Policy stored in `settings` table: `snapshot_retention_count` (integer, default 3)
- Snapshot files live at `snapshots/{output_name}/{snapshotId}/`
- Purge deletes both the DB record and the files on disk

**Failure handling:**
- If file deletion fails, log a warning — do not fail the refresh. Orphaned files can be cleaned up manually.

---

## 7. XMLTV: Snapshot vs Live Proxy

**Decision:** XMLTV is always served from the snapshot file. No live proxy mode in Core.

**Rationale:**
- Consistent with M3U/snapshot architecture — client fetches of XMLTV are decoupled from provider availability.
- Last-known-good applies: if the last refresh failed, the previous XMLTV snapshot is still served.
- Provider XMLTV is fetched during the same refresh cycle that builds the M3U snapshot.
- Live proxy would expose provider URL availability to client fetch timing, creating inconsistency between M3U and XMLTV data versions.

**Implementation:**
- Refresh cycle: fetch M3U → fetch XMLTV → build snapshot → stage → promote.
- XMLTV fetch is optional if provider has no `tvg-url` — an empty/minimal XMLTV is written to the snapshot in that case.
- `/xmltv/guidedog.xml` serves the active snapshot's `guide.xml` file directly (static file or streaming read).

---

## 8. Concurrent Refresh Policy

**Decision:** Only one refresh run at a time. Manual takes priority over scheduled.

**Rules:**
- A `SemaphoreSlim(1, 1)` in the refresh singleton prevents concurrent runs.
- If a manual refresh is triggered and one is already running: return 409 "Refresh already in progress".
- If a scheduled refresh fires and a manual (or previous scheduled) run is already in progress: log "Scheduled refresh skipped — manual refresh in progress" and exit silently. No queuing.
- UI: "Refresh Now" button is disabled while any refresh is running. Refresh state is exposed via `/status` endpoint (`is_refreshing: true/false`).
- Timeout: refresh runs have a configurable timeout (default 5 minutes). If exceeded, the run is cancelled, an error is logged, and the semaphore is released.

---

## 9. Snapshot File Path

**Decision:** Snapshot files are stored at `snapshots/{output_name}/{snapshotId}/`.

**In Core:** `output_name` is always `guidedog`. Path is always `snapshots/guidedog/{snapshotId}/`.

**Files per snapshot:**
- `playlist.m3u` — the built M3U file
- `guide.xml` — the XMLTV file
- `streamkeys.json` — streamKey → channel mapping index for fast lookup

**Snapshot DB record** stores the path it was written to, not a computed path. This means the path is stable even if configuration changes later.

**Pro — output name change:**
- When a Pro user renames a profile's output name (e.g., `guidedog` → `livingroom`), old snapshots remain at `snapshots/guidedog/...` and are served from the path stored in their DB record.
- New snapshots write to `snapshots/livingroom/...`.
- The UI shows a notice: "Output name changed. Old snapshots are at the previous path and will be purged by normal retention policy."
- No file migration required — path-in-record approach handles this automatically.
