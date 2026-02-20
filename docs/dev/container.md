# IPTVGuideDog Container Usage

This document covers local container build/run for the V1 packaging flow.

## Prerequisites

- Docker Desktop (or Docker Engine + Compose plugin)
- Repo root as current directory

## Build And Run

Start the service with the provided compose file:

```bash
docker compose up --build -d
```

Stop it:

```bash
docker compose down
```

## Ports And Volumes

Compose publishes:

- `8080` on host -> `8080` in container

Persistent named volumes:

- `guidedog_data` -> `/app/Data` (SQLite database)
- `guidedog_snapshots` -> `/app/snapshots` (snapshot files)

To remove everything including persisted data:

```bash
docker compose down -v
```

## Smoke Tests

Health:

```bash
curl -f http://localhost:8080/health
```

M3U endpoint:

```bash
curl -f http://localhost:8080/m3u/guidedog.m3u
```

Stream relay (extract first `/stream/<streamKey>` from playlist):

```bash
key_path=$(curl -fsS http://localhost:8080/m3u/guidedog.m3u | rg -o '/stream/[^[:space:]]+' -m 1)
curl -f "http://localhost:8080${key_path}" -o /dev/null
```

If no stream key is found, the playlist is likely empty because no active snapshot has been generated yet.

## Useful Commands

View status:

```bash
docker compose ps
```

View logs:

```bash
docker compose logs -f guidedog
```

Inspect health:

```bash
docker inspect --format='{{json .State.Health}}' iptvguidedog
```

## Notes

- DB schema migrations run on app startup.
- App health endpoint is `GET /health`.
- If Docker in WSL is unavailable, enable WSL integration in Docker Desktop.
