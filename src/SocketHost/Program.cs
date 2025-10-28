using IPTVGuideDog.Domain.Channels;
using IPTVGuideDog.SocketHost.Channels;
using IPTVGuideDog.SocketHost.Playlist;

var builder = WebApplication.CreateBuilder(args);

// Configure core services for delivering curated channel lists to downstream consumers.
builder.Services.AddSingleton<IChannelCatalog, InMemoryChannelCatalog>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/playlist.m3u", async (IChannelCatalog catalog, CancellationToken cancellationToken) =>
{
    var groups = await catalog.GetGroupsAsync(cancellationToken);
    return Results.Text(M3uSnapshotFormatter.From(groups), contentType: "application/x-mpegURL");
});

app.Run();
