using System.IO;

namespace WindowsUtilityPack.Services;

/// <summary>Shared profile paths. Test hosts can select an isolated profile before startup.</summary>
internal static class AppDataPaths
{
    internal const string OverrideKey = "WindowsUtilityPack.DataDirectory";

    internal static bool IsOverridden => AppContext.GetData(OverrideKey) is string;

    internal static string LocalDirectory => Resolve(Environment.SpecialFolder.LocalApplicationData);

    internal static string RoamingDirectory => Resolve(Environment.SpecialFolder.ApplicationData);

    private static string Resolve(Environment.SpecialFolder folder)
    {
        if (AppContext.GetData(OverrideKey) is string path)
        {
            if (!Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("The application data directory must be an absolute path.");

            return Path.GetFullPath(path);
        }

        return Path.Combine(Environment.GetFolderPath(folder), "WindowsUtilityPack");
    }
}
