using System.Text.Json;
using System.Text.RegularExpressions;
using IPTVGuideDog.Core.M3u;
using IPTVGuideDog.Web.Contracts.Providers;
using IPTVGuideDog.Web.Data;
using IPTVGuideDog.Web.Data.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace IPTVGuideDog.Web.Api;

public static class ProviderApiEndpoints
{
    private static readonly Regex MetadataAttributeRegex = new("(?<key>[A-Za-z0-9\\-]+)=\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapProviderApiEndpoints(this IEndpointRouteBuilder app)
    {
        var profiles = app.MapGroup("/api/v1/profiles");
        profiles.MapGet("/", ListProfilesAsync);

        var providers = app.MapGroup("/api/v1/providers");
        providers.MapGet("/", ListProvidersAsync);
        providers.MapPost("/", CreateProviderAsync);
        providers.MapGet("/{providerId}", GetProviderAsync);
        providers.MapPut("/{providerId}", UpdateProviderAsync);
        providers.MapPatch("/{providerId}/enabled", SetProviderEnabledAsync);
        providers.MapGet("/{providerId}/preview", GetPreviewAsync);
        providers.MapPost("/{providerId}/refresh-preview", RefreshPreviewAsync);
        providers.MapGet("/{providerId}/status", GetProviderStatusAsync);

        return app;
    }

    private static async Task<Ok<List<ProfileListItemDto>>> ListProfilesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var profiles = await db.Profiles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new ProfileListItemDto
                {
                    ProfileId = x.ProfileId,
                    Name = x.Name,
                    OutputName = x.OutputName,
                    MergeMode = x.MergeMode,
                    Enabled = x.Enabled,
                })
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(profiles);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TypedResults.Ok(new List<ProfileListItemDto>());
        }
    }

    private static async Task<Ok<List<ProviderDto>>> ListProvidersAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            var providers = await db.Providers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var dtos = await BuildProviderDtosAsync(db, providers, cancellationToken);
            return TypedResults.Ok(dtos);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TypedResults.Ok(new List<ProviderDto>());
        }
    }

    private static async Task<Results<Created<ProviderDto>, ValidationProblem, Conflict<string>>> CreateProviderAsync(
        CreateProviderRequest request,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var validationErrors = await ValidateProviderRequestAsync(db, request.Name, request.PlaylistUrl, request.XmltvUrl, request.HeadersJson, request.TimeoutSeconds, request.AssociateToProfileIds, null, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var now = DateTime.UtcNow;
        var provider = new Provider
        {
            ProviderId = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            Enabled = request.Enabled,
            PlaylistUrl = request.PlaylistUrl.Trim(),
            XmltvUrl = string.IsNullOrWhiteSpace(request.XmltvUrl) ? null : request.XmltvUrl.Trim(),
            HeadersJson = string.IsNullOrWhiteSpace(request.HeadersJson) ? null : request.HeadersJson,
            UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim(),
            TimeoutSeconds = request.TimeoutSeconds,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        db.Providers.Add(provider);
        ApplyProviderProfiles(db, provider.ProviderId, request.AssociateToProfileIds);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict("Provider could not be created due to a database conflict.");
        }

        var dto = (await BuildProviderDtosAsync(db, [provider], cancellationToken)).Single();
        return TypedResults.Created($"/api/v1/providers/{provider.ProviderId}", dto);
    }

    private static async Task<Results<Ok<ProviderDto>, NotFound, ValidationProblem, Conflict<string>>> UpdateProviderAsync(
        string providerId,
        UpdateProviderRequest request,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var validationErrors = await ValidateProviderRequestAsync(db, request.Name, request.PlaylistUrl, request.XmltvUrl, request.HeadersJson, request.TimeoutSeconds, request.AssociateToProfileIds, providerId, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        provider.Name = request.Name.Trim();
        provider.PlaylistUrl = request.PlaylistUrl.Trim();
        provider.XmltvUrl = string.IsNullOrWhiteSpace(request.XmltvUrl) ? null : request.XmltvUrl.Trim();
        provider.HeadersJson = string.IsNullOrWhiteSpace(request.HeadersJson) ? null : request.HeadersJson;
        provider.UserAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim();
        provider.Enabled = request.Enabled;
        provider.TimeoutSeconds = request.TimeoutSeconds;
        provider.UpdatedUtc = DateTime.UtcNow;

        var existingLinks = await db.ProfileProviders.Where(x => x.ProviderId == providerId).ToListAsync(cancellationToken);
        db.ProfileProviders.RemoveRange(existingLinks);
        ApplyProviderProfiles(db, providerId, request.AssociateToProfileIds);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Conflict("Provider could not be updated due to a database conflict.");
        }

        var dto = (await BuildProviderDtosAsync(db, [provider], cancellationToken)).Single();
        return TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<ProviderDto>, NotFound>> GetProviderAsync(string providerId, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var provider = await db.Providers.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var dto = (await BuildProviderDtosAsync(db, [provider], cancellationToken)).Single();
        return TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<ProviderEnabledResponse>, NotFound>> SetProviderEnabledAsync(
        string providerId,
        SetProviderEnabledRequest request,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        provider.Enabled = request.Enabled;
        provider.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new ProviderEnabledResponse
        {
            ProviderId = provider.ProviderId,
            Enabled = provider.Enabled,
            UpdatedUtc = provider.UpdatedUtc,
        });
    }

    private static async Task<Results<Ok<ProviderStatusDto>, NotFound>> GetProviderStatusAsync(
        string providerId,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var providerDto = (await BuildProviderDtosAsync(db, [provider], cancellationToken)).Single();

        return TypedResults.Ok(new ProviderStatusDto
        {
            ProviderId = providerId,
            LastRefresh = providerDto.LastRefresh,
            LatestSnapshots = providerDto.LatestSnapshots,
        });
    }

    private static async Task<Results<Ok<ProviderPreviewDto>, NotFound, ProblemHttpResult, ValidationProblem>> GetPreviewAsync(
        string providerId,
        int? sampleSize,
        string? groupContains,
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Providers.AsNoTracking().AnyAsync(x => x.ProviderId == providerId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var sampleSizeValue = NormalizeSampleSize(sampleSize);
        if (sampleSizeValue is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sampleSize"] = ["sampleSize must be between 1 and 50."]
            });
        }

        var latestOkFetchRun = await db.FetchRuns
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.Status == "ok")
            .OrderByDescending(x => x.StartedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestOkFetchRun is null)
        {
            return TypedResults.Problem(
                title: "No preview data available",
                detail: "No successful provider refresh exists yet for this provider.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var preview = await BuildPreviewAsync(db, providerId, latestOkFetchRun.FetchRunId, latestOkFetchRun.StartedUtc, sampleSizeValue.Value, groupContains, cancellationToken);
        return TypedResults.Ok(preview);
    }

    private static async Task<Results<Ok<ProviderPreviewDto>, NotFound, ProblemHttpResult, ValidationProblem>> RefreshPreviewAsync(
        string providerId,
        RefreshPreviewRequest request,
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        PlaylistParser parser,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
        if (provider is null)
        {
            return TypedResults.NotFound();
        }

        var sampleSizeValue = NormalizeSampleSize(request.SampleSize);
        if (sampleSizeValue is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["sampleSize"] = ["sampleSize must be between 1 and 50."]
            });
        }

        var now = DateTime.UtcNow;
        var fetchRun = new FetchRun
        {
            FetchRunId = Guid.NewGuid().ToString(),
            ProviderId = providerId,
            StartedUtc = now,
            Status = "fail",
        };

        db.FetchRuns.Add(fetchRun);
        await db.SaveChangesAsync(cancellationToken);

        string playlistContent;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));

            using var client = httpClientFactory.CreateClient();
            ApplyHeadersFromJson(client, provider.HeadersJson);
            if (!string.IsNullOrWhiteSpace(provider.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(provider.UserAgent);
            }

            playlistContent = await client.GetStringAsync(provider.PlaylistUrl, timeoutCts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            fetchRun.FinishedUtc = DateTime.UtcNow;
            fetchRun.Status = "fail";
            fetchRun.ErrorSummary = ex.Message;
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Problem(
                title: "Provider fetch failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }

        PlaylistDocument document;
        try
        {
            document = parser.Parse(playlistContent, cancellationToken);
        }
        catch (Exception ex)
        {
            fetchRun.FinishedUtc = DateTime.UtcNow;
            fetchRun.Status = "fail";
            fetchRun.ErrorSummary = ex.Message;
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Problem(
                title: "Playlist parse failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var entries = document.Entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .Select(ParseEntry)
            .ToList();

        var utcNow = DateTime.UtcNow;
        await UpsertProviderGroupsAsync(db, providerId, entries, utcNow, cancellationToken);
        await UpsertProviderChannelsAsync(db, providerId, fetchRun.FetchRunId, entries, utcNow, cancellationToken);

        fetchRun.FinishedUtc = DateTime.UtcNow;
        fetchRun.Status = "ok";
        fetchRun.ErrorSummary = null;
        fetchRun.ChannelCountSeen = entries.Count;
        fetchRun.PlaylistBytes = System.Text.Encoding.UTF8.GetByteCount(playlistContent);

        await db.SaveChangesAsync(cancellationToken);

        var preview = await BuildPreviewAsync(db, providerId, fetchRun.FetchRunId, fetchRun.StartedUtc, sampleSizeValue.Value, request.GroupContains, cancellationToken);
        return TypedResults.Ok(preview);
    }

    private static async Task<List<ProviderDto>> BuildProviderDtosAsync(ApplicationDbContext db, IReadOnlyCollection<Provider> providers, CancellationToken cancellationToken)
    {
        if (providers.Count == 0)
        {
            return [];
        }

        var providerIds = providers.Select(x => x.ProviderId).ToList();
        var profileLinks = await db.ProfileProviders
            .AsNoTracking()
            .Where(x => providerIds.Contains(x.ProviderId))
            .ToListAsync(cancellationToken);

        var latestRefreshByProvider = await db.FetchRuns
            .AsNoTracking()
            .Where(x => providerIds.Contains(x.ProviderId))
            .OrderByDescending(x => x.StartedUtc)
            .ToListAsync(cancellationToken);

        var latestRefreshLookup = latestRefreshByProvider
            .GroupBy(x => x.ProviderId)
            .ToDictionary(x => x.Key, x => x.First());

        var profileIds = profileLinks.Select(x => x.ProfileId).Distinct().ToList();
        var snapshotsByProfile = profileIds.Count == 0
            ? new Dictionary<string, Snapshot>()
            : (await db.Snapshots
                .AsNoTracking()
                .Where(x => profileIds.Contains(x.ProfileId))
                .OrderByDescending(x => x.CreatedUtc)
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ProfileId)
                .ToDictionary(x => x.Key, x => x.First());

        var linkLookup = profileLinks
            .GroupBy(x => x.ProviderId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.Priority).ThenBy(y => y.ProfileId).Select(y => y.ProfileId).ToList());

        return providers
            .OrderBy(x => x.Name)
            .Select(provider =>
            {
                linkLookup.TryGetValue(provider.ProviderId, out var associatedProfileIds);
                associatedProfileIds ??= [];

                latestRefreshLookup.TryGetValue(provider.ProviderId, out var latestRefresh);

                var latestSnapshots = associatedProfileIds
                    .Where(profileId => snapshotsByProfile.ContainsKey(profileId))
                    .Select(profileId => snapshotsByProfile[profileId])
                    .OrderByDescending(x => x.CreatedUtc)
                    .ThenBy(x => x.ProfileId)
                    .Select(x => new ProviderLatestSnapshotDto
                    {
                        SnapshotId = x.SnapshotId,
                        ProfileId = x.ProfileId,
                        Status = x.Status,
                        CreatedUtc = x.CreatedUtc,
                    })
                    .ToList();

                return new ProviderDto
                {
                    ProviderId = provider.ProviderId,
                    Name = provider.Name,
                    PlaylistUrl = provider.PlaylistUrl,
                    XmltvUrl = provider.XmltvUrl,
                    HeadersJson = provider.HeadersJson,
                    UserAgent = provider.UserAgent,
                    Enabled = provider.Enabled,
                    TimeoutSeconds = provider.TimeoutSeconds,
                    AssociatedProfileIds = associatedProfileIds,
                    LastRefresh = latestRefresh is null
                        ? null
                        : new ProviderLastRefreshDto
                        {
                            Status = latestRefresh.Status,
                            StartedUtc = latestRefresh.StartedUtc,
                            FinishedUtc = latestRefresh.FinishedUtc,
                            ErrorSummary = latestRefresh.ErrorSummary,
                            ChannelCountSeen = latestRefresh.ChannelCountSeen,
                        },
                    LatestSnapshots = latestSnapshots,
                };
            })
            .ToList();
    }

    private static async Task<Dictionary<string, string[]>> ValidateProviderRequestAsync(
        ApplicationDbContext db,
        string name,
        string playlistUrl,
        string? xmltvUrl,
        string? headersJson,
        int timeoutSeconds,
        List<string>? associateToProfileIds,
        string? providerId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["name is required."];
        }

        if (string.IsNullOrWhiteSpace(playlistUrl) || !IsValidHttpUrl(playlistUrl))
        {
            errors["playlistUrl"] = ["playlistUrl must be an absolute http/https URL."];
        }

        if (!string.IsNullOrWhiteSpace(xmltvUrl) && !IsValidHttpUrl(xmltvUrl))
        {
            errors["xmltvUrl"] = ["xmltvUrl must be an absolute http/https URL when provided."];
        }

        if (timeoutSeconds is < 1 or > 300)
        {
            errors["timeoutSeconds"] = ["timeoutSeconds must be between 1 and 300."];
        }

        if (!string.IsNullOrWhiteSpace(headersJson) && !TryValidateHeadersJson(headersJson, out var headersJsonError))
        {
            errors["headersJson"] = [headersJsonError!];
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var duplicateName = await db.Providers
                .AsNoTracking()
                .AnyAsync(x => x.Name == name.Trim() && x.ProviderId != providerId, cancellationToken);

            if (duplicateName)
            {
                errors["name"] = ["name must be unique."];
            }
        }

        var profileIds = (associateToProfileIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (profileIds.Count > 0)
        {
            var existingCount = await db.Profiles
                .AsNoTracking()
                .CountAsync(x => profileIds.Contains(x.ProfileId), cancellationToken);

            if (existingCount != profileIds.Count)
            {
                errors["associateToProfileIds"] = ["One or more profile ids do not exist."];
            }
        }

        return errors;
    }

    private static void ApplyProviderProfiles(ApplicationDbContext db, string providerId, List<string>? profileIdsInput)
    {
        var profileIds = (profileIdsInput ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < profileIds.Count; i++)
        {
            db.ProfileProviders.Add(new ProfileProvider
            {
                ProviderId = providerId,
                ProfileId = profileIds[i],
                Priority = i + 1,
                Enabled = true,
            });
        }
    }

    private static bool IsValidHttpUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool TryValidateHeadersJson(string value, out string? error)
    {
        error = null;

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "headersJson must be a JSON object of string:string pairs.";
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    error = "headersJson values must be strings.";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            error = "headersJson must be valid JSON.";
            return false;
        }
    }

    private static void ApplyHeadersFromJson(HttpClient client, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return;
        }

        using var document = JsonDocument.Parse(headersJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            client.DefaultRequestHeaders.Remove(property.Name);
            client.DefaultRequestHeaders.TryAddWithoutValidation(property.Name, value);
        }
    }

    private static int? NormalizeSampleSize(int? value)
    {
        if (value is null)
        {
            return 10;
        }

        return value is < 1 or > 50 ? null : value;
    }

    private static ParsedChannel ParseEntry(M3uEntry entry)
    {
        var metadata = entry.MetadataLines.FirstOrDefault() ?? string.Empty;
        var attributes = MetadataAttributeRegex.Matches(metadata)
            .Select(match => (Key: match.Groups["key"].Value, Value: match.Groups["value"].Value))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

        attributes.TryGetValue("tvg-id", out var tvgId);
        attributes.TryGetValue("tvg-name", out var tvgName);
        attributes.TryGetValue("tvg-logo", out var logoUrl);
        attributes.TryGetValue("group-title", out var groupTitleAttr);

        var groupTitle = !string.IsNullOrWhiteSpace(entry.Group)
            ? entry.Group!.Trim()
            : string.IsNullOrWhiteSpace(groupTitleAttr) ? null : groupTitleAttr.Trim();

        var providerChannelKey = NormalizeProviderChannelKey(tvgId);
        var displayName = string.IsNullOrWhiteSpace(entry.Title)
            ? (string.IsNullOrWhiteSpace(tvgName) ? "Unnamed Channel" : tvgName.Trim())
            : entry.Title.Trim();

        return new ParsedChannel
        {
            ProviderChannelKey = providerChannelKey,
            DisplayName = displayName,
            TvgId = string.IsNullOrWhiteSpace(tvgId) ? null : tvgId.Trim(),
            TvgName = string.IsNullOrWhiteSpace(tvgName) ? null : tvgName.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim(),
            StreamUrl = entry.Url!.Trim(),
            GroupTitle = groupTitle,
        };
    }

    private static string? NormalizeProviderChannelKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task UpsertProviderGroupsAsync(
        ApplicationDbContext db,
        string providerId,
        IReadOnlyList<ParsedChannel> entries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var groupNames = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupTitle))
            .Select(x => x.GroupTitle!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingGroups = await db.ProviderGroups
            .Where(x => x.ProviderId == providerId)
            .ToListAsync(cancellationToken);

        var byName = existingGroups.ToDictionary(x => x.RawName, StringComparer.Ordinal);

        foreach (var groupName in groupNames)
        {
            if (byName.TryGetValue(groupName, out var existing))
            {
                existing.LastSeenUtc = now;
                existing.Active = true;
                continue;
            }

            db.ProviderGroups.Add(new ProviderGroup
            {
                ProviderGroupId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                RawName = groupName,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                Active = true,
            });
        }

        foreach (var group in existingGroups)
        {
            if (!groupNames.Contains(group.RawName, StringComparer.Ordinal))
            {
                group.Active = false;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertProviderChannelsAsync(
        ApplicationDbContext db,
        string providerId,
        string fetchRunId,
        IReadOnlyList<ParsedChannel> entries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var groupLookup = await db.ProviderGroups
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .ToDictionaryAsync(x => x.RawName, x => x.ProviderGroupId, StringComparer.Ordinal, cancellationToken);

        var keys = entries.Where(x => x.ProviderChannelKey is not null).Select(x => x.ProviderChannelKey!).Distinct(StringComparer.Ordinal).ToList();

        var existingByKey = keys.Count == 0
            ? new Dictionary<string, ProviderChannel>(StringComparer.Ordinal)
            : await db.ProviderChannels
                .Where(x => x.ProviderId == providerId && x.ProviderChannelKey != null && keys.Contains(x.ProviderChannelKey))
                .ToDictionaryAsync(x => x.ProviderChannelKey!, StringComparer.Ordinal, cancellationToken);

        var nullKeyChannels = await db.ProviderChannels
            .Where(x => x.ProviderId == providerId && x.ProviderChannelKey == null)
            .ToListAsync(cancellationToken);

        var existingByComposite = new Dictionary<string, ProviderChannel>(StringComparer.Ordinal);
        foreach (var channel in nullKeyChannels)
        {
            var composite = BuildNullKeyComposite(channel.DisplayName, channel.StreamUrl, channel.GroupTitle);
            if (!existingByComposite.ContainsKey(composite))
            {
                existingByComposite[composite] = channel;
            }
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var providerGroupId = entry.GroupTitle is not null && groupLookup.TryGetValue(entry.GroupTitle, out var foundGroupId)
                ? foundGroupId
                : null;

            ProviderChannel channel;
            if (entry.ProviderChannelKey is not null)
            {
                if (!existingByKey.TryGetValue(entry.ProviderChannelKey, out channel!))
                {
                    channel = new ProviderChannel
                    {
                        ProviderChannelId = Guid.NewGuid().ToString(),
                        ProviderId = providerId,
                        ProviderChannelKey = entry.ProviderChannelKey,
                        FirstSeenUtc = now,
                    };

                    db.ProviderChannels.Add(channel);
                    existingByKey[entry.ProviderChannelKey] = channel;
                }
            }
            else
            {
                var composite = BuildNullKeyComposite(entry.DisplayName, entry.StreamUrl, entry.GroupTitle);
                if (!existingByComposite.TryGetValue(composite, out channel!))
                {
                    channel = new ProviderChannel
                    {
                        ProviderChannelId = Guid.NewGuid().ToString(),
                        ProviderId = providerId,
                        ProviderChannelKey = null,
                        FirstSeenUtc = now,
                    };

                    db.ProviderChannels.Add(channel);
                    existingByComposite[composite] = channel;
                }
            }

            channel.ProviderChannelKey = NormalizeProviderChannelKey(entry.ProviderChannelKey);
            channel.DisplayName = entry.DisplayName;
            channel.TvgId = entry.TvgId;
            channel.TvgName = entry.TvgName;
            channel.LogoUrl = entry.LogoUrl;
            channel.StreamUrl = entry.StreamUrl;
            channel.GroupTitle = entry.GroupTitle;
            channel.ProviderGroupId = providerGroupId;
            channel.IsEvent = false;
            channel.EventStartUtc = null;
            channel.EventEndUtc = null;
            channel.LastSeenUtc = now;
            channel.Active = true;
            channel.LastFetchRunId = fetchRunId;

            seenIds.Add(channel.ProviderChannelId);
        }

        var activeChannels = await db.ProviderChannels
            .Where(x => x.ProviderId == providerId && x.Active)
            .ToListAsync(cancellationToken);

        foreach (var channel in activeChannels)
        {
            if (!seenIds.Contains(channel.ProviderChannelId))
            {
                channel.Active = false;
            }
        }
    }

    private static string BuildNullKeyComposite(string displayName, string streamUrl, string? groupTitle)
        => $"{displayName}\u001f{streamUrl}\u001f{groupTitle}";

    private static async Task<ProviderPreviewDto> BuildPreviewAsync(
        ApplicationDbContext db,
        string providerId,
        string fetchRunId,
        DateTime fetchStartedUtc,
        int sampleSize,
        string? groupContains,
        CancellationToken cancellationToken)
    {
        var channels = await db.ProviderChannels
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.LastFetchRunId == fetchRunId)
            .Select(x => new PreviewChannelProjection
            {
                ProviderChannelId = x.ProviderChannelId,
                DisplayName = x.DisplayName,
                TvgId = x.TvgId,
                GroupName = (x.ProviderGroup != null ? x.ProviderGroup.RawName : x.GroupTitle) ?? string.Empty,
                StreamUrl = x.StreamUrl,
            })
            .ToListAsync(cancellationToken);

        var normalizedGroupFilter = string.IsNullOrWhiteSpace(groupContains) ? null : groupContains.Trim();

        var grouped = channels
            .Select(x => new PreviewChannelProjection
            {
                ProviderChannelId = x.ProviderChannelId,
                DisplayName = x.DisplayName,
                TvgId = x.TvgId,
                GroupName = string.IsNullOrWhiteSpace(x.GroupName) ? "(Ungrouped)" : x.GroupName!,
                StreamUrl = x.StreamUrl,
            })
            .Where(x => normalizedGroupFilter is null || x.GroupName.Contains(normalizedGroupFilter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.GroupName, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var previewGroups = grouped
            .Select(group => new ProviderPreviewGroupDto
            {
                GroupName = group.Key,
                ChannelCount = group.Count(),
                SampleChannels = group
                    .OrderBy(x => x.DisplayName, StringComparer.Ordinal)
                    .ThenBy(x => x.ProviderChannelId, StringComparer.Ordinal)
                    .Take(sampleSize)
                    .Select(x => new ProviderPreviewSampleChannelDto
                    {
                        ProviderChannelId = x.ProviderChannelId,
                        DisplayName = x.DisplayName,
                        TvgId = x.TvgId,
                        HasStreamUrl = !string.IsNullOrWhiteSpace(x.StreamUrl),
                        StreamUrlRedacted = RedactStreamUrl(x.StreamUrl),
                    })
                    .ToList(),
            })
            .ToList();

        return new ProviderPreviewDto
        {
            ProviderId = providerId,
            PreviewGeneratedUtc = DateTime.UtcNow,
            Source = new ProviderPreviewSourceDto
            {
                Kind = "latest-successful-provider-refresh",
                FetchRunId = fetchRunId,
                FetchStartedUtc = fetchStartedUtc,
            },
            Totals = new ProviderPreviewTotalsDto
            {
                GroupCount = previewGroups.Count,
                ChannelCount = previewGroups.Sum(x => x.ChannelCount),
            },
            Groups = previewGroups,
        };
    }

    private static string? RedactStreamUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri.GetLeftPart(UriPartial.Path);
    }

    private sealed class ParsedChannel
    {
        public string? ProviderChannelKey { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? TvgId { get; init; }
        public string? TvgName { get; init; }
        public string? LogoUrl { get; init; }
        public string StreamUrl { get; init; } = string.Empty;
        public string? GroupTitle { get; init; }
    }

    private sealed class PreviewChannelProjection
    {
        public string ProviderChannelId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? TvgId { get; init; }
        public string GroupName { get; init; } = string.Empty;
        public string? StreamUrl { get; init; }
    }
}
