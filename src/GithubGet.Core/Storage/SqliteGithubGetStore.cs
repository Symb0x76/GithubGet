using System.Text.Json;
using GithubGet.Core.Models;
using Microsoft.Data.Sqlite;

namespace GithubGet.Core.Storage;

public sealed class SqliteGithubGetStore : IGithubGetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _dbPath;
    private bool _initialized;
    private const string EtagPrefix = "etag:";

    public SqliteGithubGetStore(string? dbPath = null)
    {
        _dbPath = string.IsNullOrWhiteSpace(dbPath) ? StorePaths.DatabasePath : dbPath;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        StorePaths.EnsureDirectories();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE IF NOT EXISTS Subscriptions (
    Id TEXT PRIMARY KEY,
    Owner TEXT NOT NULL,
    Repo TEXT NOT NULL,
    DisplayName TEXT,
    IncludePrerelease INTEGER NOT NULL,
    AssetRulesJson TEXT NOT NULL,
    InstallKind TEXT NOT NULL,
    SilentArgs TEXT,
    MsiArgs TEXT,
    RequireAdmin INTEGER NOT NULL,
    TimeoutSeconds INTEGER NOT NULL,
    AllowReboot INTEGER NOT NULL,
    ExpectedPublisher TEXT,
    PreInstallScriptEnabled INTEGER NOT NULL DEFAULT 0,
    PreInstallScriptPath TEXT,
    PreInstallScriptArgs TEXT,
    PreInstallScriptRequireAdmin INTEGER NOT NULL DEFAULT 0,
    LastSeenReleaseId INTEGER,
    LastSeenTag TEXT,
    LastCheckedAtUtc TEXT
);
CREATE TABLE IF NOT EXISTS Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS UpdateEvents (
    Id TEXT PRIMARY KEY,
    SubscriptionId TEXT NOT NULL,
    ReleaseId INTEGER NOT NULL,
    Tag TEXT NOT NULL,
    Title TEXT,
    PublishedAtUtc TEXT NOT NULL,
    HtmlUrl TEXT NOT NULL,
    BodyMarkdown TEXT,
    SelectedAssetJson TEXT,
    DownloadedFilePath TEXT,
    ScriptPath TEXT,
    ScriptExitCode INTEGER,
    ScriptStandardOutput TEXT,
    ScriptStandardError TEXT,
    InstallExitCode INTEGER,
    InstallStandardOutput TEXT,
    InstallStandardError TEXT,
    ProcessingMessage TEXT,
    ProcessedAtUtc TEXT,
    State TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);
""";

        await command.ExecuteNonQueryAsync(ct);
        await EnsureSchemaColumnsAsync(connection, ct);
        _initialized = true;
    }

    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var list = new List<Subscription>();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Subscriptions";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapSubscription(reader));
        }

        return list;
    }

    public async Task<Subscription?> GetSubscriptionAsync(string id, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Subscriptions WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapSubscription(reader);
        }

        return null;
    }

    public async Task UpsertSubscriptionAsync(Subscription subscription, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO Subscriptions (
    Id, Owner, Repo, DisplayName, IncludePrerelease, AssetRulesJson, InstallKind,
    SilentArgs, MsiArgs, RequireAdmin, TimeoutSeconds, AllowReboot, ExpectedPublisher,
    PreInstallScriptEnabled, PreInstallScriptPath, PreInstallScriptArgs, PreInstallScriptRequireAdmin,
    LastSeenReleaseId, LastSeenTag, LastCheckedAtUtc
) VALUES (
    $id, $owner, $repo, $displayName, $includePrerelease, $assetRulesJson, $installKind,
    $silentArgs, $msiArgs, $requireAdmin, $timeoutSeconds, $allowReboot, $expectedPublisher,
    $preInstallScriptEnabled, $preInstallScriptPath, $preInstallScriptArgs, $preInstallScriptRequireAdmin,
    $lastSeenReleaseId, $lastSeenTag, $lastCheckedAtUtc
)
ON CONFLICT(Id) DO UPDATE SET
    Owner = excluded.Owner,
    Repo = excluded.Repo,
    DisplayName = excluded.DisplayName,
    IncludePrerelease = excluded.IncludePrerelease,
    AssetRulesJson = excluded.AssetRulesJson,
    InstallKind = excluded.InstallKind,
    SilentArgs = excluded.SilentArgs,
    MsiArgs = excluded.MsiArgs,
    RequireAdmin = excluded.RequireAdmin,
    TimeoutSeconds = excluded.TimeoutSeconds,
    AllowReboot = excluded.AllowReboot,
    ExpectedPublisher = excluded.ExpectedPublisher,
    PreInstallScriptEnabled = excluded.PreInstallScriptEnabled,
    PreInstallScriptPath = excluded.PreInstallScriptPath,
    PreInstallScriptArgs = excluded.PreInstallScriptArgs,
    PreInstallScriptRequireAdmin = excluded.PreInstallScriptRequireAdmin,
    LastSeenReleaseId = excluded.LastSeenReleaseId,
    LastSeenTag = excluded.LastSeenTag,
    LastCheckedAtUtc = excluded.LastCheckedAtUtc;
""";

        command.Parameters.AddWithValue("$id", subscription.Id);
        command.Parameters.AddWithValue("$owner", subscription.Owner);
        command.Parameters.AddWithValue("$repo", subscription.Repo);
        command.Parameters.AddWithValue("$displayName", (object?)subscription.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$includePrerelease", subscription.IncludePrerelease ? 1 : 0);
        command.Parameters.AddWithValue("$assetRulesJson", JsonSerializer.Serialize(subscription.AssetRules, JsonOptions));
        command.Parameters.AddWithValue("$installKind", subscription.InstallKind.ToString());
        command.Parameters.AddWithValue("$silentArgs", (object?)subscription.SilentArgs ?? DBNull.Value);
        command.Parameters.AddWithValue("$msiArgs", (object?)subscription.MsiArgs ?? DBNull.Value);
        command.Parameters.AddWithValue("$requireAdmin", subscription.RequireAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$timeoutSeconds", subscription.TimeoutSeconds);
        command.Parameters.AddWithValue("$allowReboot", subscription.AllowReboot ? 1 : 0);
        command.Parameters.AddWithValue("$expectedPublisher", (object?)subscription.ExpectedPublisher ?? DBNull.Value);
        command.Parameters.AddWithValue("$preInstallScriptEnabled", subscription.PreInstallScriptEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$preInstallScriptPath", (object?)subscription.PreInstallScriptPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$preInstallScriptArgs", (object?)subscription.PreInstallScriptArgs ?? DBNull.Value);
        command.Parameters.AddWithValue("$preInstallScriptRequireAdmin", subscription.PreInstallScriptRequireAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$lastSeenReleaseId", subscription.LastSeenReleaseId.HasValue ? subscription.LastSeenReleaseId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$lastSeenTag", (object?)subscription.LastSeenTag ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastCheckedAtUtc", subscription.LastCheckedAtUtc.HasValue ? subscription.LastCheckedAtUtc.Value.ToString("O") : DBNull.Value);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteSubscriptionAsync(string id, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Subscriptions WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AddUpdateEventAsync(UpdateEvent updateEvent, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO UpdateEvents (
    Id, SubscriptionId, ReleaseId, Tag, Title, PublishedAtUtc, HtmlUrl, BodyMarkdown,
    SelectedAssetJson, DownloadedFilePath, ScriptPath, ScriptExitCode, ScriptStandardOutput, ScriptStandardError,
    InstallExitCode, InstallStandardOutput, InstallStandardError, ProcessingMessage, ProcessedAtUtc,
    State, CreatedAtUtc
) VALUES (
    $id, $subscriptionId, $releaseId, $tag, $title, $publishedAtUtc, $htmlUrl, $bodyMarkdown,
    $selectedAssetJson, $downloadedFilePath, $scriptPath, $scriptExitCode, $scriptStandardOutput, $scriptStandardError,
    $installExitCode, $installStandardOutput, $installStandardError, $processingMessage, $processedAtUtc,
    $state, $createdAtUtc
);
""";

        command.Parameters.AddWithValue("$id", updateEvent.Id);
        command.Parameters.AddWithValue("$subscriptionId", updateEvent.SubscriptionId);
        command.Parameters.AddWithValue("$releaseId", updateEvent.ReleaseId);
        command.Parameters.AddWithValue("$tag", updateEvent.Tag);
        command.Parameters.AddWithValue("$title", (object?)updateEvent.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$publishedAtUtc", updateEvent.PublishedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$htmlUrl", updateEvent.HtmlUrl);
        command.Parameters.AddWithValue("$bodyMarkdown", (object?)updateEvent.BodyMarkdown ?? DBNull.Value);
        command.Parameters.AddWithValue("$selectedAssetJson", updateEvent.SelectedAsset is null
            ? DBNull.Value
            : JsonSerializer.Serialize(updateEvent.SelectedAsset, JsonOptions));
        command.Parameters.AddWithValue("$downloadedFilePath", (object?)updateEvent.DownloadedFilePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$scriptPath", (object?)updateEvent.ScriptPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$scriptExitCode", updateEvent.ScriptExitCode.HasValue ? updateEvent.ScriptExitCode.Value : DBNull.Value);
        command.Parameters.AddWithValue("$scriptStandardOutput", (object?)updateEvent.ScriptStandardOutput ?? DBNull.Value);
        command.Parameters.AddWithValue("$scriptStandardError", (object?)updateEvent.ScriptStandardError ?? DBNull.Value);
        command.Parameters.AddWithValue("$installExitCode", updateEvent.InstallExitCode.HasValue ? updateEvent.InstallExitCode.Value : DBNull.Value);
        command.Parameters.AddWithValue("$installStandardOutput", (object?)updateEvent.InstallStandardOutput ?? DBNull.Value);
        command.Parameters.AddWithValue("$installStandardError", (object?)updateEvent.InstallStandardError ?? DBNull.Value);
        command.Parameters.AddWithValue("$processingMessage", (object?)updateEvent.ProcessingMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$processedAtUtc", updateEvent.ProcessedAtUtc.HasValue ? updateEvent.ProcessedAtUtc.Value.ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue("$state", updateEvent.State.ToString());
        command.Parameters.AddWithValue("$createdAtUtc", updateEvent.CreatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateUpdateEventStateAsync(string id, UpdateState state, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE UpdateEvents SET State = $state WHERE Id = $id";
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<UpdateEvent>> GetUpdateEventsAsync(string? subscriptionId = null, int limit = 200, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var list = new List<UpdateEvent>();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            command.CommandText = "SELECT * FROM UpdateEvents ORDER BY CreatedAtUtc DESC LIMIT $limit";
        }
        else
        {
            command.CommandText = "SELECT * FROM UpdateEvents WHERE SubscriptionId = $subscriptionId ORDER BY CreatedAtUtc DESC LIMIT $limit";
            command.Parameters.AddWithValue("$subscriptionId", subscriptionId);
        }

        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapUpdateEvent(reader));
        }

        return list;
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        var result = await command.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO Settings (Key, Value)
VALUES ($key, $value)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
""";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public Task<string?> GetEtagAsync(string key, CancellationToken ct = default)
    {
        return GetSettingAsync($"{EtagPrefix}{key}", ct);
    }

    public Task SetEtagAsync(string key, string value, CancellationToken ct = default)
    {
        return SetSettingAsync($"{EtagPrefix}{key}", value, ct);
    }

    private static async Task EnsureSchemaColumnsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await EnsureColumnAsync(connection, "Subscriptions", "PreInstallScriptEnabled", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync(connection, "Subscriptions", "PreInstallScriptPath", "TEXT", ct);
        await EnsureColumnAsync(connection, "Subscriptions", "PreInstallScriptArgs", "TEXT", ct);
        await EnsureColumnAsync(connection, "Subscriptions", "PreInstallScriptRequireAdmin", "INTEGER NOT NULL DEFAULT 0", ct);

        await EnsureColumnAsync(connection, "UpdateEvents", "DownloadedFilePath", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ScriptPath", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ScriptExitCode", "INTEGER", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ScriptStandardOutput", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ScriptStandardError", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "InstallExitCode", "INTEGER", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "InstallStandardOutput", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "InstallStandardError", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ProcessingMessage", "TEXT", ct);
        await EnsureColumnAsync(connection, "UpdateEvents", "ProcessedAtUtc", "TEXT", ct);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken ct)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (
            ex.SqliteErrorCode == 1 &&
            ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static Subscription MapSubscription(SqliteDataReader reader)
    {
        var assetRulesJson = reader["AssetRulesJson"] as string;
        AssetRuleSet assetRules;
        try
        {
            assetRules = string.IsNullOrWhiteSpace(assetRulesJson)
                ? AssetRuleSet.Default
                : JsonSerializer.Deserialize<AssetRuleSet>(assetRulesJson, JsonOptions) ?? AssetRuleSet.Default;
        }
        catch
        {
            assetRules = AssetRuleSet.Default;
        }

        return new Subscription
        {
            Id = reader["Id"] as string ?? Guid.NewGuid().ToString("N"),
            Owner = reader["Owner"] as string ?? string.Empty,
            Repo = reader["Repo"] as string ?? string.Empty,
            DisplayName = reader["DisplayName"] as string,
            IncludePrerelease = Convert.ToInt32(reader["IncludePrerelease"]) == 1,
            AssetRules = assetRules,
            InstallKind = Enum.TryParse<InstallKind>(reader["InstallKind"] as string, out var kind) ? kind : InstallKind.Auto,
            SilentArgs = reader["SilentArgs"] as string,
            MsiArgs = reader["MsiArgs"] as string,
            RequireAdmin = Convert.ToInt32(reader["RequireAdmin"]) == 1,
            TimeoutSeconds = Convert.ToInt32(reader["TimeoutSeconds"]),
            AllowReboot = Convert.ToInt32(reader["AllowReboot"]) == 1,
            ExpectedPublisher = reader["ExpectedPublisher"] as string,
            PreInstallScriptEnabled = Convert.ToInt32(reader["PreInstallScriptEnabled"]) == 1,
            PreInstallScriptPath = reader["PreInstallScriptPath"] as string,
            PreInstallScriptArgs = reader["PreInstallScriptArgs"] as string,
            PreInstallScriptRequireAdmin = Convert.ToInt32(reader["PreInstallScriptRequireAdmin"]) == 1,
            LastSeenReleaseId = reader["LastSeenReleaseId"] is DBNull ? null : Convert.ToInt64(reader["LastSeenReleaseId"]),
            LastSeenTag = reader["LastSeenTag"] as string,
            LastCheckedAtUtc = reader["LastCheckedAtUtc"] is DBNull ? null : DateTimeOffset.Parse(reader["LastCheckedAtUtc"] as string ?? string.Empty)
        };
    }

    private static UpdateEvent MapUpdateEvent(SqliteDataReader reader)
    {
        var selectedJson = reader["SelectedAssetJson"] as string;
        SelectedAsset? selected = null;
        if (!string.IsNullOrWhiteSpace(selectedJson))
        {
            try
            {
                selected = JsonSerializer.Deserialize<SelectedAsset>(selectedJson, JsonOptions);
            }
            catch
            {
                selected = null;
            }
        }

        return new UpdateEvent
        {
            Id = reader["Id"] as string ?? Guid.NewGuid().ToString("N"),
            SubscriptionId = reader["SubscriptionId"] as string ?? string.Empty,
            ReleaseId = Convert.ToInt64(reader["ReleaseId"]),
            Tag = reader["Tag"] as string ?? string.Empty,
            Title = reader["Title"] as string,
            PublishedAtUtc = DateTimeOffset.Parse(reader["PublishedAtUtc"] as string ?? string.Empty),
            HtmlUrl = reader["HtmlUrl"] as string ?? string.Empty,
            BodyMarkdown = reader["BodyMarkdown"] as string,
            SelectedAsset = selected,
            DownloadedFilePath = reader["DownloadedFilePath"] as string,
            ScriptPath = reader["ScriptPath"] as string,
            ScriptExitCode = reader["ScriptExitCode"] is DBNull ? null : Convert.ToInt32(reader["ScriptExitCode"]),
            ScriptStandardOutput = reader["ScriptStandardOutput"] as string,
            ScriptStandardError = reader["ScriptStandardError"] as string,
            InstallExitCode = reader["InstallExitCode"] is DBNull ? null : Convert.ToInt32(reader["InstallExitCode"]),
            InstallStandardOutput = reader["InstallStandardOutput"] as string,
            InstallStandardError = reader["InstallStandardError"] as string,
            ProcessingMessage = reader["ProcessingMessage"] as string,
            ProcessedAtUtc = reader["ProcessedAtUtc"] is DBNull ? null : DateTimeOffset.Parse(reader["ProcessedAtUtc"] as string ?? string.Empty),
            State = Enum.TryParse<UpdateState>(reader["State"] as string, out var state) ? state : UpdateState.New,
            CreatedAtUtc = DateTimeOffset.Parse(reader["CreatedAtUtc"] as string ?? string.Empty)
        };
    }
}
