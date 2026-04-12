using Microsoft.Data.Sqlite;
using System.IO;

namespace WindowsUtilityPack.Services;

/// <summary>
/// SQLite-backed local persistence host for cross-tool data.
/// </summary>
public sealed class AppDataStoreService : IAppDataStoreService
{
    private readonly object _initLock = new();
    private volatile bool _initialized;

    public string DatabasePath { get; }

    public AppDataStoreService(string? databasePath = null)
    {
        DatabasePath = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsUtilityPack",
                "appdata.db")
            : databasePath;
    }

    /// <inheritdoc />
    public void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = CreateConnection();
            ApplyMigrations(connection);
            _initialized = true;
        }
    }

    /// <inheritdoc />
    public SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static void ApplyMigrations(SqliteConnection connection)
    {
        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var currentVersion = Convert.ToInt32(versionCommand.ExecuteScalar() ?? 0);

        if (currentVersion < 1)
        {
            ExecuteNonQuery(connection, @"
CREATE TABLE IF NOT EXISTS activity_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    category TEXT NOT NULL,
    action TEXT NOT NULL,
    details TEXT NOT NULL,
    is_sensitive INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS workspace_profiles (
    name TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    startup_tool_key TEXT NOT NULL,
    pinned_tool_keys_json TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_activity_log_timestamp ON activity_log(timestamp_utc DESC);
CREATE INDEX IF NOT EXISTS idx_activity_log_category ON activity_log(category);
CREATE INDEX IF NOT EXISTS idx_workspace_profiles_updated ON workspace_profiles(updated_utc DESC);
");

            ExecuteNonQuery(connection, "PRAGMA user_version = 1;");
            currentVersion = 1;
        }

        if (currentVersion < 2)
        {
            ExecuteNonQuery(connection, @"
CREATE TABLE IF NOT EXISTS clipboard_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    captured_utc TEXT NOT NULL,
    content TEXT NOT NULL,
    content_kind TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_clipboard_history_captured ON clipboard_history(captured_utc DESC);
");

            ExecuteNonQuery(connection, "PRAGMA user_version = 2;");
            currentVersion = 2;
        }

        if (currentVersion < 3)
        {
            ExecuteNonQuery(connection, @"
CREATE TABLE IF NOT EXISTS automation_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    trigger_type TEXT NOT NULL,
    threshold REAL NOT NULL,
    cooldown_minutes INTEGER NOT NULL,
    enabled INTEGER NOT NULL,
    action_type TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_automation_rules_enabled ON automation_rules(enabled);
CREATE INDEX IF NOT EXISTS idx_automation_rules_updated ON automation_rules(updated_utc DESC);
");

            ExecuteNonQuery(connection, "PRAGMA user_version = 3;");
            currentVersion = 3;
        }

        if (currentVersion < 4)
        {
            ExecuteNonQuery(connection, @"
CREATE TABLE IF NOT EXISTS ssh_profiles (
    name TEXT PRIMARY KEY,
    host TEXT NOT NULL,
    port INTEGER NOT NULL,
    username TEXT NOT NULL,
    private_key_path TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_ssh_profiles_updated ON ssh_profiles(updated_utc DESC);
");

            ExecuteNonQuery(connection, "PRAGMA user_version = 4;");
            currentVersion = 4;
        }

        if (currentVersion < 5)
        {
            if (!ColumnExists(connection, "automation_rules", "action_target"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE automation_rules ADD COLUMN action_target TEXT NOT NULL DEFAULT '';");
            }

            if (!ColumnExists(connection, "automation_rules", "action_parameters_json"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE automation_rules ADD COLUMN action_parameters_json TEXT NOT NULL DEFAULT '{}';");
            }

            ExecuteNonQuery(connection, "PRAGMA user_version = 5;");
        }
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var existingName = reader.GetString(1);
            if (string.Equals(existingName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
