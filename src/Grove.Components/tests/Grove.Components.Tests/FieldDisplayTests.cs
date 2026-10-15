using System.Text.Json.Nodes;
using Grove.Components.Content;
using Grove.Components.Editors;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Grove.Components.Tests;

// Rework step 7 (docs/blazor-display.md): field, title, list and bag displays as components, so
// the site path no longer falls back to their Razor views; the BagPart editor edits each
// contained item through its type's editor tree.
public sealed class FieldDisplayTests
{
    [Fact]
    public async Task ALinkFieldRendersItsChosenTextAndRefusesAScriptUrl()
    {
        var safe = await RenderAsync<LinkFieldDisplay>(new() { ["Url"] = "https://example.com", ["LinkText"] = "Example", ["Target"] = "_blank", ["FieldClass"] = "article-link" });
        var unsafeLink = await RenderAsync<LinkFieldDisplay>(new() { ["Url"] = "javascript:alert(1)", ["LinkText"] = "x" });

        Assert.Contains("field-name-article-link", safe, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com\"", safe, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", safe, StringComparison.Ordinal);
        Assert.Contains("href=\"#\"", unsafeLink, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReferenceFieldsShowTheirLabelAndTheirResolvedItems()
    {
        var picker = await RenderAsync<OptionPickerFieldDisplay>(new() { ["Label"] = "Related", ["Items"] = new List<DisplayLinkItem> { new("About", "about") } });
        var users = await RenderAsync<UserPickerFieldDisplay>(new() { ["Label"] = "Owners", ["UserNames"] = new[] { "ada", "linus" } });
        var empty = await RenderAsync<DateFieldDisplay>(new() { ["Value"] = null });

        Assert.Contains("<span class=\"name\">Related</span>", picker, StringComparison.Ordinal);
        Assert.Contains("href=\"about\"", picker, StringComparison.Ordinal);
        Assert.Contains("ada", users, StringComparison.Ordinal);
        Assert.Contains("linus", users, StringComparison.Ordinal);
        Assert.Equal(string.Empty, empty.Trim());
    }

    [Fact]
    public async Task AYoutubeFieldEmbedsOnlyYoutubeAddresses()
    {
        Assert.Contains("<iframe", await RenderAsync<YoutubeFieldDisplay>(new() { ["EmbeddedAddress"] = "https://www.youtube.com/embed/abc" }), StringComparison.Ordinal);
        Assert.DoesNotContain("<iframe", await RenderAsync<YoutubeFieldDisplay>(new() { ["EmbeddedAddress"] = "https://evil.example/embed" }), StringComparison.Ordinal);
    }

    [Fact]
    public void ANestedEditorTreeEditsTheContainedItem()
    {
        var template = new DisplayNode
        {
            Id = "editor",
            Component = "ContentEditor",
            Properties = new(StringComparer.OrdinalIgnoreCase) { ["ContentType"] = "Line", ["Content"] = new JsonObject { ["ContentItemId"] = "new" } },
        };

        var copy = BagPartEditor.WithContent(template, new JsonObject { ["ContentItemId"] = "line-1", ["ContentType"] = "Line" });

        Assert.Equal("line-1", copy.Properties["Content"]!["ContentItemId"]!.GetValue<string>());
        Assert.Equal("new", template.Properties["Content"]!["ContentItemId"]!.GetValue<string>());
        Assert.Equal("Line", copy.Properties["ContentType"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(1, 10, 95, 3, new[] { 1, 2, 3, 4 })]
    [InlineData(5, 10, 95, 2, new[] { 3, 4, 5, 6, 7 })]
    [InlineData(99, 10, 95, 1, new[] { 9, 10 })]
    public void APagerShowsTheCurrentPageAndItsNeighbours(int page, int pageSize, int total, int quantity, int[] expected) =>
        Assert.Equal(expected, Content.Pager.Pages(page, pageSize, total, quantity));

    [Fact]
    public async Task APagerLinksPagesByItsQueryKeyAndHidesWithOnePage()
    {
        var pager = await RenderAsync<Content.Pager>(new() { ["Page"] = 2, ["PageSize"] = 10, ["TotalItemCount"] = 25 });
        var single = await RenderAsync<Content.Pager>(new() { ["Page"] = 1, ["PageSize"] = 10, ["TotalItemCount"] = 5 });

        Assert.Contains("href=\"?pagenum=1\"", pager, StringComparison.Ordinal);
        Assert.Contains("href=\"?pagenum=3\"", pager, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", pager, StringComparison.Ordinal);
        Assert.Equal(string.Empty, single.Trim());
    }

    [Fact]
    public async Task ATaxonomyRendersItsTermTreeAndAGeoPointLinksToAMap()
    {
        var taxonomy = await RenderAsync<TaxonomyPartDisplay>(new()
        {
            ["Terms"] = new List<TaxonomyTermNode> { new("Space", "tags/space", [new("Mars", "tags/mars", [])]) },
        });
        var point = await RenderAsync<GeoPointFieldDisplay>(new() { ["Latitude"] = 51.5m, ["Longitude"] = -0.12m });

        Assert.Contains("href=\"tags/mars\"", taxonomy, StringComparison.Ordinal);
        Assert.Equal(2, taxonomy.Split("<ul>").Length - 1);
        Assert.Contains("openstreetmap.org", point, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters) where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
