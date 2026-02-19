# V1 Sections 2 and 3 Endpoint Contracts

Scope: `docs/dev/v1_checklist.md` sections 2 and 3.

Goals covered:
- Provider list/add/edit/enabled toggle
- Associate provider to profile (V1 UI may restrict to one profile, API supports many)
- Last refresh status + snapshot timestamp in provider list details
- Preview groups from latest provider refresh data
- Display channel counts + first N channels
- Refresh and preview in one action

Design constraints:
- Uses existing schema (`providers`, `profiles`, `profile_providers`, `fetch_runs`, `provider_groups`, `provider_channels`, `snapshots`)
- No schema changes required for these contracts
- Preview is read-only (no lineup shaping here)
- Parser reuse: `IPTVGuideDog.Core.M3u.PlaylistParser`

## Conventions

- Base path: `/api/v1`
- JSON content type: `application/json`
- Date/time fields: ISO-8601 UTC (example: `2026-02-18T20:45:31Z`)
- IDs: existing DB ids as strings (UUID stored as TEXT)
- Validation errors: `400` with ProblemDetails-style payload
- Booleans:
  - API uses `true/false`
  - DB stores `INTEGER 0/1`

---

## 1) List Profiles (for provider association picker)

### GET `/api/v1/profiles`

Response `200 OK`:
```json
[
  {
    "profileId": "default-profile-id",
    "name": "default",
    "outputName": "default",
    "mergeMode": "single",
    "enabled": true
  }
]
```

Notes:
- Ordered by `name` ascending.
- Read-only helper endpoint for section 2 UI.

---

## 2) List Providers

### GET `/api/v1/providers`

Response `200 OK`:
```json
[
  {
    "providerId": "provider-id",
    "name": "Provider A",
    "playlistUrl": "https://example.com/playlist.m3u",
    "xmltvUrl": "https://example.com/guide.xml",
    "headersJson": null,
    "userAgent": null,
    "enabled": true,
    "timeoutSeconds": 20,
    "associatedProfileIds": ["default-profile-id"],
    "lastRefresh": {
      "status": "ok",
      "startedUtc": "2026-02-18T19:00:00Z",
      "finishedUtc": "2026-02-18T19:00:05Z",
      "errorSummary": null,
      "channelCountSeen": 1243
    },
    "latestSnapshots": [
      {
        "snapshotId": "snapshot-id",
        "profileId": "default-profile-id",
        "status": "active",
        "createdUtc": "2026-02-18T19:05:00Z"
      }
    ]
  }
]
```

Rules:
- `associatedProfileIds`: derived from `profile_providers`.
- `lastRefresh`: latest `fetch_runs` record per provider (nullable if none).
- `latestSnapshots`: always an array. If no snapshots exist for associated profiles, return `[]`.
- `latestSnapshots`: for each associated profile, include that profile's most recent snapshot across any status.
- Ordering: `createdUtc DESC` (tie-breaker `profileId ASC`).

---

## 3) Create Provider

### POST `/api/v1/providers`

Request:
```json
{
  "name": "Provider A",
  "playlistUrl": "https://example.com/playlist.m3u",
  "xmltvUrl": "https://example.com/guide.xml",
  "headersJson": null,
  "userAgent": null,
  "enabled": true,
  "timeoutSeconds": 20,
  "associateToProfileIds": ["default-profile-id"]
}
```

Validation:
- `name`: required, non-empty, unique
- `playlistUrl`: required, absolute http/https URL
- `xmltvUrl`: optional, if present must be absolute http/https URL
- `headersJson`: optional; if present must be a JSON object with string keys and string values (`{ "Header-Name": "value" }`)
- `userAgent`: optional
- `timeoutSeconds`: required, range `1..300`
- `associateToProfileIds`: optional; each id must exist

Response `201 Created`:
- `Location: /api/v1/providers/{providerId}`
- Body: same shape as provider item from list endpoint.

Errors:
- `400` invalid payload
- `409` duplicate provider name

---

## 4) Get Provider

### GET `/api/v1/providers/{providerId}`

Response `200 OK`:
- Same shape as provider item from list endpoint.

Errors:
- `404` provider not found

---

## 5) Update Provider

### PUT `/api/v1/providers/{providerId}`

Request:
```json
{
  "name": "Provider A",
  "playlistUrl": "https://example.com/playlist.m3u",
  "xmltvUrl": "https://example.com/guide.xml",
  "headersJson": null,
  "userAgent": null,
  "enabled": true,
  "timeoutSeconds": 20,
  "associateToProfileIds": ["default-profile-id"]
}
```

Behavior:
- Full update for editable fields.
- Replaces provider-profile associations to match `associateToProfileIds`.

Response `200 OK`:
- Updated provider DTO.

Errors:
- `400` invalid payload
- `404` provider not found
- `409` duplicate provider name

---

## 6) Toggle Provider Enabled

### PATCH `/api/v1/providers/{providerId}/enabled`

Request:
```json
{
  "enabled": false
}
```

Response `200 OK`:
```json
{
  "providerId": "provider-id",
  "enabled": false,
  "updatedUtc": "2026-02-18T20:00:00Z"
}
```

Errors:
- `400` invalid payload
- `404` provider not found

---

## 7) Preview Latest Provider Data (read-only)

### GET `/api/v1/providers/{providerId}/preview?sampleSize=10&groupContains=sports`

Query params:
- `sampleSize` optional, default `10`, min `1`, max `50`
- `groupContains` optional text filter (case-insensitive on group name)

Response `200 OK`:
```json
{
  "providerId": "provider-id",
  "previewGeneratedUtc": "2026-02-18T20:10:00Z",
  "source": {
    "kind": "latest-successful-provider-refresh",
    "fetchRunId": "fetch-run-id",
    "fetchStartedUtc": "2026-02-18T19:00:00Z"
  },
  "totals": {
    "groupCount": 12,
    "channelCount": 1243
  },
  "groups": [
    {
      "groupName": "USA | News",
      "channelCount": 87,
      "sampleChannels": [
        {
          "providerChannelId": "pc-1",
          "displayName": "CNN US",
          "tvgId": "cnn.us",
          "hasStreamUrl": true,
          "streamUrlRedacted": "https://example.com/stream/…"
        }
      ]
    }
  ]
}
```

Rules:
- Uses `provider_groups` + `provider_channels` from the latest successful (`fetch_runs.status = 'ok'`) provider refresh.
- If no successful refresh exists, return `409`.
- Deterministic ordering:
  1. `display_name` ascending
  2. `provider_channel_id` ascending
- Do not return full upstream stream URLs by default.
- `hasStreamUrl` is required on each sample channel.
- `streamUrlRedacted` is optional and, when present, MUST:
  - remove query string parameters
  - remove URI user-info credentials
  - preserve scheme + host + path only

Errors:
- `404` provider not found
- `409` no successful refresh exists yet for provider (ProblemDetails response)

---

## 8) Refresh and Preview

### POST `/api/v1/providers/{providerId}/refresh-preview`

Request:
```json
{
  "sampleSize": 10,
  "groupContains": null
}
```

Behavior:
1. Fetch playlist for provider URL.
2. Parse with `PlaylistParser` from `IPTVGuideDog.Core`.
3. Upsert `provider_groups` and `provider_channels` for provider.
   - Normalize empty/whitespace `provider_channel_key` to NULL before upsert.
4. Insert `fetch_runs` with status `ok`/`fail`.
5. Return preview payload.

Response `200 OK`:
- Same payload as GET preview.

Errors:
- `404` provider not found
- `400` invalid request (ProblemDetails response)
- `502` upstream fetch failure
- `500` parse/persistence failure

---

## 9) Optional Lightweight Status Endpoint

### GET `/api/v1/providers/{providerId}/status`

Response `200 OK`:
```json
{
  "providerId": "provider-id",
  "lastRefresh": {
    "status": "ok",
    "startedUtc": "2026-02-18T19:00:00Z",
    "finishedUtc": "2026-02-18T19:00:05Z",
    "errorSummary": null,
    "channelCountSeen": 1243
  },
  "latestSnapshots": [
    {
      "snapshotId": "snapshot-id",
      "profileId": "default-profile-id",
      "status": "active",
      "createdUtc": "2026-02-18T19:05:00Z"
    }
  ]
}
```

Errors:
- `404` provider not found

---

## Out of Scope

- Scheduled refresh orchestration (section 4)
- Snapshot activation lifecycle (section 4)
- Public serving endpoints (`/m3u`, `/xmltv`, `/stream`) (section 5)

---

## Naming Alignment Notes

- API field naming for provider activation state is `enabled` everywhere.
- Endpoint naming is `/enabled` (not `/active`).
- UI bindings should use `enabled` to match API and DB schema (`providers.enabled`).
- API supports many profile associations via `associateToProfileIds`.
- V1 UI can constrain selection to one profile while still calling the same API contract.
