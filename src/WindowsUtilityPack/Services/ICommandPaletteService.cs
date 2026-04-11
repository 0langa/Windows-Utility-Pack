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

    /// <summary>
    /// Records command execution to improve future result relevance.
    /// </summary>
    void RecordExecution(string itemId);
}

/// <summary>
/// Default command palette indexer using registered tools and shell commands.
/// </summary>
public sealed class CommandPaletteService : ICommandPaletteService
{
    private readonly Dictionary<string, int> _executionCount = new(StringComparer.OrdinalIgnoreCase);

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
            IconGlyph = "\uE80F", // Home glyph
            ShortcutHint = "Ctrl+H",
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
            IconGlyph = "\uE713", // Settings glyph
            ShortcutHint = "Ctrl+,",
        },
        new CommandPaletteItem
        {
            Id = "shell:popout-current-tool",
            Title = "Open Current Tool in New Window",
            Subtitle = "Detach the current tool into a separate window",
            Category = "Shell",
            CommandKey = "popout-current-tool",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["detach", "window", "popout", "multitask"],
            IconGlyph = "\uE946", // MiniExpand glyph
        },
        new CommandPaletteItem
        {
            Id = "shell:quick-screenshot",
            Title = "Quick Screenshot",
            Subtitle = "Capture screenshot immediately",
            Category = "Shell",
            CommandKey = "quick-screenshot",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["capture", "image", "screen"],
            ShortcutHint = "Ctrl+Shift+S",
        },
        new CommandPaletteItem
        {
            Id = "shell:open-screenshot-annotator",
            Title = "Open Screenshot Annotator",
            Subtitle = "Open the screenshot workflow tool",
            Category = "Shell",
            CommandKey = "open-screenshot-annotator",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["annotation", "redaction", "markup", "image"],
        },
        new CommandPaletteItem
        {
            Id = "shell:toggle-main-window",
            Title = "Toggle Main Window",
            Subtitle = "Show or hide the main window",
            Category = "Shell",
            CommandKey = "toggle-main-window",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["show", "hide", "tray", "background"],
            ShortcutHint = "Ctrl+Shift+Space",
        },
        new CommandPaletteItem
        {
            Id = "shell:open-clipboard-manager",
            Title = "Open Clipboard Manager",
            Subtitle = "Open clipboard history and quick reuse tools",
            Category = "Shell",
            CommandKey = "open-clipboard-manager",
            Kind = CommandPaletteItemKind.ShellAction,
            Keywords = ["clipboard", "history", "paste"],
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
                IconGlyph = tool.IconGlyph,
                Icon = tool.Icon,
            });
        }

        limit = Math.Clamp(limit, 1, 50);
        if (string.IsNullOrWhiteSpace(query))
        {
            return items
                .OrderBy(i => i.Kind == CommandPaletteItemKind.ShellAction ? 0 : 1)
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

    public void RecordExecution(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        _executionCount[itemId] = _executionCount.TryGetValue(itemId, out var count)
            ? count + 1
            : 1;
    }

    private int Score(CommandPaletteItem item, IReadOnlyList<string> terms)
    {
        var score = 0;
        var title = item.Title.ToLowerInvariant();
        var subtitle = item.Subtitle.ToLowerInvariant();
        var category = item.Category.ToLowerInvariant();
        var shortcut = item.ShortcutHint.ToLowerInvariant();
        if (_executionCount.TryGetValue(item.Id, out var usage))
        {
            score += Math.Min(usage * 8, 80);
        }

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

            if (!string.IsNullOrWhiteSpace(shortcut) && shortcut.Contains(term, StringComparison.Ordinal))
            {
                score += 20;
            }

            if (item.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score += 30;
            }
        }

        return score;
    }
}
