using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class CommandPaletteServiceTests
{
    // ── Empty / null query ────────────────────────────────────────────────────

    [Fact]
    public void Search_WithoutQuery_IncludesCoreShellActions()
    {
        var service = new CommandPaletteService();

        var items = service.Search(string.Empty, limit: 10);

        Assert.Contains(items, i => i.CommandKey == "open-settings");
        Assert.Contains(items, i => i.CommandKey == "home");
        Assert.Contains(items, i => i.CommandKey == "popout-current-tool");
    }

    [Fact]
    public void Search_WithNullQuery_ReturnsDefaultItems()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 10);

        Assert.NotEmpty(items);
    }

    [Fact]
    public void Search_WithWhitespaceQuery_TreatsAsEmpty()
    {
        var service = new CommandPaletteService();

        var whitespaceItems = service.Search("   ", limit: 10);
        var emptyItems      = service.Search("",    limit: 10);

        // Both should return the same set (order-insensitive).
        var wKeys = whitespaceItems.Select(i => i.CommandKey).OrderBy(k => k).ToList();
        var eKeys = emptyItems    .Select(i => i.CommandKey).OrderBy(k => k).ToList();
        Assert.Equal(eKeys, wKeys);
    }

    // ── Query filtering ───────────────────────────────────────────────────────

    [Fact]
    public void Search_WithQuery_FiltersByTitleAndKeywords()
    {
        var service = new CommandPaletteService();

        var items = service.Search("settings", limit: 10);

        Assert.NotEmpty(items);
        Assert.Equal("open-settings", items[0].CommandKey);
    }

    [Fact]
    public void Search_WithQuery_HomeReturnsFirstShellAction()
    {
        var service = new CommandPaletteService();

        var items = service.Search("home", limit: 5);

        Assert.Contains(items, i => i.CommandKey == "home");
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var service = new CommandPaletteService();

        var upperItems = service.Search("SETTINGS", limit: 5);
        var lowerItems = service.Search("settings", limit: 5);

        Assert.Equal(upperItems.Count, lowerItems.Count);
        Assert.True(upperItems.Count > 0);
    }

    [Fact]
    public void Search_NoMatchingQuery_ReturnsEmpty()
    {
        var service = new CommandPaletteService();

        var items = service.Search("xyzzy_no_such_tool_ever", limit: 10);

        Assert.Empty(items);
    }

    // ── Limit clamping ────────────────────────────────────────────────────────

    [Fact]
    public void Search_RespectsLimit()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 3);

        Assert.True(items.Count <= 3);
    }

    [Fact]
    public void Search_LimitBelowOne_ClampedToOne()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 0);

        Assert.Single(items);
    }

    [Fact]
    public void Search_LimitAboveFifty_ClampedToFifty()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 999);

        Assert.True(items.Count <= 50);
    }

    // ── Icon fields ───────────────────────────────────────────────────────────

    [Fact]
    public void Search_ShellHomeItem_HasIconGlyph()
    {
        var service = new CommandPaletteService();

        var items = service.Search("home", limit: 5);
        var home  = items.FirstOrDefault(i => i.CommandKey == "home");

        Assert.NotNull(home);
        Assert.False(string.IsNullOrEmpty(home!.IconGlyph));
    }

    [Fact]
    public void Search_ShellSettingsItem_HasIconGlyph()
    {
        var service = new CommandPaletteService();

        var items    = service.Search("settings", limit: 5);
        var settings = items.FirstOrDefault(i => i.CommandKey == "open-settings");

        Assert.NotNull(settings);
        Assert.False(string.IsNullOrEmpty(settings!.IconGlyph));
    }

    // ── Item structure ────────────────────────────────────────────────────────

    [Fact]
    public void Search_AllItems_HaveNonEmptyIdAndTitle()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 50);

        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id),    $"Item with title '{item.Title}' has empty Id.");
            Assert.False(string.IsNullOrWhiteSpace(item.Title), $"Item with id '{item.Id}' has empty Title.");
        }
    }

    [Fact]
    public void Search_AllItems_HaveNonEmptyCommandKey()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 50);

        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CommandKey),
                $"Item '{item.Id}' has empty CommandKey.");
        }
    }

    [Fact]
    public void Search_AllItems_HaveValidKind()
    {
        var service = new CommandPaletteService();

        var items = service.Search(null, limit: 50);

        foreach (var item in items)
        {
            Assert.True(
                Enum.IsDefined(typeof(CommandPaletteItemKind), item.Kind),
                $"Item '{item.Id}' has invalid Kind: {item.Kind}");
        }
    }

    // ── Scoring / relevance ───────────────────────────────────────────────────

    [Fact]
    public void Search_ExactTitleMatch_RanksHigherThanSubstringMatch()
    {
        var service = new CommandPaletteService();

        // "home" exactly matches the "Go to Home" item's title segment and keyword.
        var items = service.Search("settings", limit: 5);

        // The "Open Settings" shell item should rank first.
        Assert.Equal("open-settings", items[0].CommandKey);
    }

    [Fact]
    public void Search_MultiTermQuery_FiltersConjunctively()
    {
        var service = new CommandPaletteService();

        // "open settings" — both terms must score something.
        var items = service.Search("open settings", limit: 5);

        Assert.NotEmpty(items);
    }

    // ── CommandPaletteItem model ──────────────────────────────────────────────

    [Fact]
    public void CommandPaletteItem_IconGlyph_DefaultsToEmpty()
    {
        var item = new CommandPaletteItem { Id = "x", Title = "X", Kind = CommandPaletteItemKind.Tool };
        Assert.Equal(string.Empty, item.IconGlyph);
    }

    [Fact]
    public void CommandPaletteItem_Icon_DefaultsToEmpty()
    {
        var item = new CommandPaletteItem { Id = "x", Title = "X", Kind = CommandPaletteItemKind.Tool };
        Assert.Equal(string.Empty, item.Icon);
    }

    [Fact]
    public void CommandPaletteItem_CanSetIconGlyphAndIcon()
    {
        var item = new CommandPaletteItem
        {
            Id = "x", Title = "X",
            Kind = CommandPaletteItemKind.Tool,
            IconGlyph = "\uE713",
            Icon = "⚙️",
        };

        Assert.Equal("\uE713", item.IconGlyph);
        Assert.Equal("⚙️",     item.Icon);
    }
}
