using System.Text;
using System.Text.Json;
using IPTVGuideDog.Web.Application;
using IPTVGuideDog.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace IPTVGuideDog.Web.Api;

public static class CompatibilityEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static IEndpointRouteBuilder MapCompatibilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/m3u/guidedog.m3u", ServeM3uAsync).AllowAnonymous();
        app.MapGet("/xmltv/guidedog.xml", ServeXmltvAsync).AllowAnonymous();
        app.MapGet("/stream/{streamKey}", ServeStreamAsync).AllowAnonymous();
        app.MapGet("/status", ServeStatusAsync).AllowAnonymous();

        return app;
    }

    // -------------------------------------------------------------------------
    // GET /m3u/guidedog.m3u
    // -------------------------------------------------------------------------

    private static async Task ServeM3uAsync(HttpContext ctx, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await db.Snapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Status == "active", cancellationToken);

            if (snapshot is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.Headers.Append("Retry-After", "60");
                await ctx.Response.WriteAsync("No active snapshot available. Waiting for first refresh.", cancellationToken);
                return;
            }

            List<ChannelIndexEntry> channels;
            try
            {
                var json = await File.ReadAllTextAsync(snapshot.ChannelIndexPath, cancellationToken);
                channels = JsonSerializer.Deserialize<List<ChannelIndexEntry>>(json, JsonOptions) ?? [];
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.Headers.Append("Retry-After", "30");
                await ctx.Response.WriteAsync("Active snapshot data is unavailable.", cancellationToken);
                return;
            }

            var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var xmltvUrl = $"{baseUrl}/xmltv/guidedog.xml";

            ctx.Response.ContentType = "application/x-mpegurl; charset=utf-8";

            var sb = new StringBuilder();
            sb.Append($"#EXTM3U url-tvg=\"{xmltvUrl}\" x-tvg-url=\"{xmltvUrl}\"\n");

            foreach (var channel in channels)
            {
                sb.Append(BuildExtInf(channel));
                sb.Append('\n');
                sb.Append($"{baseUrl}/stream/{channel.StreamKey}");
                sb.Append('\n');
            }

            await ctx.Response.WriteAsync(sb.ToString(), Encoding.UTF8, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected before response completed
        }
    }

    // -------------------------------------------------------------------------
    // GET /xmltv/guidedog.xml
    // -------------------------------------------------------------------------

    private static async Task ServeXmltvAsync(HttpContext ctx, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await db.Snapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Status == "active", cancellationToken);

            if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.XmltvPath))
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.Headers.Append("Retry-After", "60");
                await ctx.Response.WriteAsync("No active snapshot available.", cancellationToken);
                return;
            }

            if (!File.Exists(snapshot.XmltvPath))
            {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.Headers.Append("Retry-After", "30");
                await ctx.Response.WriteAsync("Active snapshot data is unavailable.", cancellationToken);
                return;
            }

            ctx.Response.ContentType = "application/xml; charset=utf-8";
            await ctx.Response.SendFileAsync(snapshot.XmltvPath, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected before response completed
        }
    }

    // -------------------------------------------------------------------------
    // GET /stream/{streamKey}
    // Security: MUST relay — MUST NOT redirect. Provider URLs embed credentials.
    // -------------------------------------------------------------------------

    private static async Task ServeStreamAsync(
        string streamKey,
        HttpContext ctx,
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var snapshot = await db.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Status == "active", cancellationToken);

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ChannelIndexPath))
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        List<ChannelIndexEntry> channels;
        try
        {
            var json = await File.ReadAllTextAsync(snapshot.ChannelIndexPath, cancellationToken);
            channels = JsonSerializer.Deserialize<List<ChannelIndexEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var entry = channels.FirstOrDefault(x => x.StreamKey == streamKey);
        if (entry is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Get active provider for custom headers/UserAgent
        var provider = await db.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Enabled, cancellationToken);

        try
        {
            // Named client has Timeout.InfiniteTimeSpan — live streams run indefinitely
            using var client = httpClientFactory.CreateClient("stream-relay");

            if (provider is not null)
            {
                ProviderFetcher.ApplyHeadersFromJson(client, provider.HeadersJson);
                if (!string.IsNullOrWhiteSpace(provider.UserAgent))
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(provider.UserAgent);
                }
            }

            // Forward Range header if present (enables VOD/catchup seeking)
            if (ctx.Request.Headers.TryGetValue("Range", out var rangeValue))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Range", rangeValue.ToArray());
            }

            using var upstreamResponse = await client.GetAsync(
                entry.StreamUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            ctx.Response.StatusCode = (int)upstreamResponse.StatusCode;

            if (upstreamResponse.Content.Headers.ContentType is not null)
            {
                ctx.Response.ContentType = upstreamResponse.Content.Headers.ContentType.ToString();
            }

            if (upstreamResponse.Content.Headers.ContentLength.HasValue)
            {
                ctx.Response.ContentLength = upstreamResponse.Content.Headers.ContentLength.Value;
            }

            await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            await upstreamStream.CopyToAsync(ctx.Response.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — normal for live streams
        }
        catch (HttpRequestException)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
            }
        }
    }

    // -------------------------------------------------------------------------
    // GET /status
    // -------------------------------------------------------------------------

    private static async Task ServeStatusAsync(HttpContext ctx, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var activeSnapshot = await db.Snapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Status == "active", cancellationToken);

            var activeProvider = await db.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive && x.Enabled, cancellationToken);

            FetchRunInfo? lastRefresh = null;
            if (activeProvider is not null)
            {
                var run = await db.FetchRuns
                    .AsNoTracking()
                    .Where(x => x.ProviderId == activeProvider.ProviderId)
                    .OrderByDescending(x => x.StartedUtc)
                    .FirstOrDefaultAsync(cancellationToken);

                if (run is not null)
                {
                    lastRefresh = new FetchRunInfo(run.Status, run.StartedUtc, run.FinishedUtc, run.ChannelCountSeen, run.ErrorSummary);
                }
            }

            var status = new StatusResponse(
                Status: activeSnapshot is not null ? "ok" : "no_active_snapshot",
                ActiveProvider: activeProvider is null ? null : new ActiveProviderInfo(activeProvider.ProviderId, activeProvider.Name),
                ActiveSnapshot: activeSnapshot is null ? null : new ActiveSnapshotInfo(
                    activeSnapshot.SnapshotId,
                    activeSnapshot.ProfileId,
                    activeSnapshot.CreatedUtc,
                    activeSnapshot.ChannelCountPublished),
                LastRefresh: lastRefresh);

            ctx.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(ctx.Response.Body, status, JsonOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected before response completed
        }
    }

    // -------------------------------------------------------------------------
    // M3U helpers
    // -------------------------------------------------------------------------

    private static string BuildExtInf(ChannelIndexEntry channel)
    {
        var sb = new StringBuilder("#EXTINF:-1");

        if (!string.IsNullOrWhiteSpace(channel.TvgId))
        {
            sb.Append($" tvg-id=\"{channel.TvgId}\"");
        }

        var tvgName = !string.IsNullOrWhiteSpace(channel.TvgName) ? channel.TvgName : channel.DisplayName;
        sb.Append($" tvg-name=\"{tvgName}\"");

        if (!string.IsNullOrWhiteSpace(channel.LogoUrl))
        {
            sb.Append($" tvg-logo=\"{channel.LogoUrl}\"");
        }

        if (!string.IsNullOrWhiteSpace(channel.GroupTitle))
        {
            sb.Append($" group-title=\"{channel.GroupTitle}\"");
        }

        if (channel.TvgChno.HasValue)
        {
            sb.Append($" tvg-chno=\"{channel.TvgChno.Value}\"");
        }

        sb.Append($",{channel.DisplayName}");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Status response records
    // -------------------------------------------------------------------------

    private sealed record StatusResponse(
        string Status,
        ActiveProviderInfo? ActiveProvider,
        ActiveSnapshotInfo? ActiveSnapshot,
        FetchRunInfo? LastRefresh);

    private sealed record ActiveProviderInfo(string ProviderId, string Name);

    private sealed record ActiveSnapshotInfo(
        string SnapshotId,
        string ProfileId,
        DateTime CreatedUtc,
        int ChannelCountPublished);

    private sealed record FetchRunInfo(
        string Status,
        DateTime StartedUtc,
        DateTime? FinishedUtc,
        int? ChannelCountSeen,
        string? ErrorSummary);
}
