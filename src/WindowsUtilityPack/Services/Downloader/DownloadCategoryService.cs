namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Resolves category rules for jobs based on URL domain and file extension.</summary>
public sealed class DownloadCategoryService : IDownloadCategoryService
{
    public DownloadCategoryRule ResolveCategory(string url, string extension, DownloaderSettings settings)
    {
        var categories = settings.Categories.Count > 0
            ? settings.Categories
            : DownloadCategoryRule.CreateDefaults();

        var host = string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }

        foreach (var category in categories)
        {
            if (category.Extensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }

            if (!string.IsNullOrWhiteSpace(host)
                && category.DomainContains.Any(pattern => host.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        return categories.FirstOrDefault(cat => cat.Name.Equals("Mixed", StringComparison.OrdinalIgnoreCase))
            ?? new DownloadCategoryRule { Name = "Mixed" };
    }
}
