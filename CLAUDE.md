# Claude Code Instructions — IPTVGuideDog

## Documentation First

Before implementing anything in C#, ASP.NET Core, Blazor, or Entity Framework Core:
- Use the **mslearn MCP** (`microsoft_docs_search` / `microsoft_docs_fetch`) to confirm current .NET 10 / C# 13 APIs, patterns, and best practices.
- Do not rely on training data alone for framework APIs — the mslearn MCP has current docs.

Before using any NuGet library (MudBlazor, etc.):
- Use the **context7 MCP** (`resolve-library-id` then `get-library-docs`) to pull the latest documentation for that library.
- Always check the version in the .csproj and match docs to that version.

## Project Context

- **Runtime**: .NET 10, C# 13, ASP.NET Core 10
- **UI**: Blazor Server (Interactive Server rendering) + MudBlazor 8.x
- **DB**: SQLite via EF Core 10 (migrations in `src/IPTVGuideDog.Web`)
- **Architecture**: Single process — Blazor UI + REST API + compatibility endpoints + background service

## Key Design Rules

- Stream relay is a security contract: `/stream/<streamKey>` MUST relay — NEVER HTTP 302 redirect to provider URL (provider URLs embed credentials).
- Single active provider at a time. Enforced by partial unique index on `providers.is_active = 1`.
- Compatibility endpoints (`/m3u/`, `/xmltv/`, `/stream/`, `/health`, `/status`) are always `[AllowAnonymous]`.
- Snapshot files live at `{ContentRootPath}/Data/snapshots/guidedog/{snapshotId}/`.
- Output name is locked to `"guidedog"` in Core. Do not add code paths that change this.

## Style

- Use minimal APIs for all new REST endpoints (not controller classes).
- Use `TypedResults` for all endpoint return types.
- Use `IServiceScopeFactory` + `CreateAsyncScope()` when background services need scoped services.
- `SaveChangesAsync(CancellationToken.None)` for error/failure state writes that must survive cancellation.
- No `// comments` unless logic is non-obvious. No docstrings on internal code.
