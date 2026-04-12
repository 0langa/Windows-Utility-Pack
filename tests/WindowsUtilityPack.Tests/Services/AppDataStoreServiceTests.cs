using Microsoft.Data.Sqlite;
using System.IO;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class AppDataStoreServiceTests
{
    [Fact]
    public void EnsureInitialized_CreatesDatabaseAndTables()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new AppDataStoreService(path);

            service.EnsureInitialized();

            Assert.True(File.Exists(path));

            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('activity_log', 'workspace_profiles');";
            using var reader = command.ExecuteReader();

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                found.Add(reader.GetString(0));
            }

            Assert.Contains("activity_log", found);
            Assert.Contains("workspace_profiles", found);

            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(automation_rules);";
            using var schemaReader = pragma.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (schemaReader.Read())
            {
                columns.Add(schemaReader.GetString(1));
            }

            Assert.Contains("action_target", columns);
            Assert.Contains("action_parameters_json", columns);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static string GetTempDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"wup-tests-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // SQLite may still hold a short-lived file lock during finalizer cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore best-effort temp cleanup failures in tests.
        }
    }
}
