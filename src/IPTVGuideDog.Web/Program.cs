using IPTVGuideDog.Core.M3u;
using IPTVGuideDog.Web.Api;
using IPTVGuideDog.Web.Application;
using IPTVGuideDog.Web.Components;
using IPTVGuideDog.Web.Components.Account;
using IPTVGuideDog.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddValidation();
builder.Services.AddHttpClient();
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});
builder.Services.AddSingleton<PlaylistParser>();
builder.Services.AddSingleton<EnvironmentVariableService>();
builder.Services.AddScoped<ConfigYamlService>();

// Named HttpClient for stream relay — no body timeout (live streams run indefinitely)
builder.Services.AddHttpClient("stream-relay", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.Configure<RefreshOptions>(builder.Configuration.GetSection("IPTVGuideDog:Refresh"));
builder.Services.Configure<SnapshotOptions>(builder.Configuration.GetSection("IPTVGuideDog:Snapshot"));
builder.Services.AddSingleton<ProviderFetcher>();
builder.Services.AddScoped<SnapshotBuilder>();
builder.Services.AddSingleton<SnapshotRefreshService>();
builder.Services.AddSingleton<IRefreshTrigger>(sp => sp.GetRequiredService<SnapshotRefreshService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SnapshotRefreshService>());

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddMudServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    EnsureSqliteMigrationHistoryBaseline(db);
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapProviderApiEndpoints();
app.MapCompatibilityEndpoints();
app.MapHealthChecks("/health");

app.Run();

static void EnsureSqliteMigrationHistoryBaseline(ApplicationDbContext db)
{
    if (!db.Database.IsSqlite())
    {
        return;
    }

    var connection = (SqliteConnection)db.Database.GetDbConnection();
    var mustClose = connection.State == System.Data.ConnectionState.Closed;
    if (mustClose)
    {
        connection.Open();
    }

    var hasAspNetRoles = TableExists(connection, "AspNetRoles");
    if (!hasAspNetRoles)
    {
        if (mustClose)
        {
            connection.Close();
        }
        return;
    }

    var currentMigrations = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    var hasHistoryTable = TableExists(connection, "__EFMigrationsHistory");
    var existingHistory = hasHistoryTable ? ReadMigrationHistory(connection) : [];
    var hasAnyMatchingHistory = existingHistory.Any(currentMigrations.Contains);
    var needsHistoryRepair = !hasAnyMatchingHistory;
    if (!needsHistoryRepair)
    {
        if (mustClose)
        {
            connection.Close();
        }
        return;
    }

    var appliedMigrations = new List<string>();
    appliedMigrations.Add("00000000000000_CreateIdentitySchema");

    if (TableExists(connection, "providers") && TableExists(connection, "profiles"))
    {
        appliedMigrations.Add("20260218202605_AddGuideDogSchema");
    }

    if (TableHasColumn(connection, "providers", "is_active"))
    {
        appliedMigrations.Add("20260220161132_AddProviderIsActive");
    }

    if (TableHasColumn(connection, "providers", "config_source_path"))
    {
        appliedMigrations.Add("20260220200000_AddConfigYamlSupport");
    }

    if (appliedMigrations.Count == 0)
    {
        if (mustClose)
        {
            connection.Close();
        }
        return;
    }

    using var transaction = connection.BeginTransaction();
    if (hasHistoryTable)
    {
        using var clearHistory = connection.CreateCommand();
        clearHistory.Transaction = transaction;
        clearHistory.CommandText = """DELETE FROM "__EFMigrationsHistory";""";
        clearHistory.ExecuteNonQuery();
    }

    using (var createHistory = connection.CreateCommand())
    {
        createHistory.Transaction = transaction;
        createHistory.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """;
        createHistory.ExecuteNonQuery();
    }

    foreach (var migrationId in appliedMigrations)
    {
        using var insertHistory = connection.CreateCommand();
        insertHistory.Transaction = transaction;
        insertHistory.CommandText = """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ($migrationId, $productVersion);
            """;
        insertHistory.Parameters.AddWithValue("$migrationId", migrationId);
        insertHistory.Parameters.AddWithValue("$productVersion", "10.0.0");
        insertHistory.ExecuteNonQuery();
    }

    transaction.Commit();
    if (mustClose)
    {
        connection.Close();
    }
}

static bool TableExists(SqliteConnection connection, string tableName)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT EXISTS (
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName
        );
        """;
    command.Parameters.AddWithValue("$tableName", tableName);
    var result = command.ExecuteScalar();
    return Convert.ToInt32(result) == 1;
}

static bool TableHasColumn(SqliteConnection connection, string tableName, string columnName)
{
    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

static List<string> ReadMigrationHistory(SqliteConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT "MigrationId"
        FROM "__EFMigrationsHistory"
        ORDER BY "MigrationId";
        """;
    using var reader = command.ExecuteReader();
    var results = new List<string>();
    while (reader.Read())
    {
        results.Add(reader.GetString(0));
    }

    return results;
}

