using Microsoft.Data.Sqlite;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides access to the shared local application database.
/// </summary>
public interface IAppDataStoreService
{
    /// <summary>
    /// Absolute path to the SQLite database file.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Ensures the database file exists and all migrations are applied.
    /// </summary>
    void EnsureInitialized();

    /// <summary>
    /// Creates an opened SQLite connection scoped to one operation.
    /// </summary>
    SqliteConnection CreateConnection();
}