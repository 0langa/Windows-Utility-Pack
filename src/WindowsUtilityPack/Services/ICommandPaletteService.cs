using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Produces searchable command items for the shell command palette.
/// </summary>
public interface ICommandPaletteService
{
    /// <summary>
    /// Returns matching command items ordered by relevance.
    /// </summary>
    IReadOnlyList<CommandPaletteItem> Search(string? query, int limit = 20);
}

/// <summary>
/// Default command palette indexer using registered tools and shell commands.
/// </summary>
public sealed class CommandPaletteService : ICommandPaletteService
{
    private static readonly IReadOnlyList<CommandPaletteItem> ShellItems =
    [
        new CommandPaletteItem
        {
            Id = "shell:home",
            Title = "Go to Home",
            Subtitle = "Navigate to the dashboard",
            Category = "Shell",
            CommandKey = "home",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["dashboard", "start", "home"],
        },
        new CommandPaletteItem
        {
            Id = "shell:settings",
            Title = "Open Settings",
            Subtitle = "Open shell settings window",
            Category = "Shell",
            CommandKey = "open-settings",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["preferences", "theme", "options"],
        },
        new CommandPaletteItem
        {
            Id = "shell:popout-current-tool",
            Title = "Open Current Tool In New Window",
            Subtitle = "Detach the current tool into a separate window",
            Category = "Shell",
            CommandKey = "popout-current-tool",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["detach", "window", "popout", "multitask"],
        },
    ];

    /// <inheritdoc />
    public IReadOnlyList<CommandPaletteItem> Search(string? query, int limit = 20)
    {
        var items = new List<CommandPaletteItem>(ShellItems.Count + Tools.ToolRegistry.All.Count);
        items.AddRange(ShellItems);

        foreach (var tool in Tools.ToolRegistry.GetDisplayTools())
        {
            items.Add(new CommandPaletteItem
            {
                Id = $"tool:{tool.Key}",
                Title = tool.Name,
                Subtitle = tool.Description,
                Category = tool.Category,
                CommandKey = tool.Key,
                Kind = CommandPaletteItemKind.Tool,
                Keywords = tool.Keywords,
            });
        }

        limit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(query))
        {
            return items
                .OrderBy(i => i.Kind)
                .ThenBy(i => i.Category)
                .ThenBy(i => i.Title)
                .Take(limit)
                .ToList();
        }

        var terms = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToArray();

        return items
            .Select(i => new { Item = i, Score = Score(i, terms) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Title)
            .Take(limit)
            .Select(x => x.Item)
            .ToList();
    }

    private static int Score(CommandPaletteItem item, IReadOnlyList<string> terms)
    {
        var score = 0;
        var title = item.Title.ToLowerInvariant();
        var subtitle = item.Subtitle.ToLowerInvariant();
        var category = item.Category.ToLowerInvariant();

        foreach (var term in terms)
        {
            if (title.Equals(term, StringComparison.Ordinal))
            {
                score += 80;
                continue;
            }

            if (title.Contains(term, StringComparison.Ordinal))
            {
                score += 50;
            }

            if (subtitle.Contains(term, StringComparison.Ordinal))
            {
                score += 20;
            }

            if (category.Contains(term, StringComparison.Ordinal))
            {
                score += 10;
            }

            if (item.CommandKey.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
            }

            if (item.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 30;
            }
        }

        return score;
    }
}