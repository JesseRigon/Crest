using System.Text.Json.Nodes;
using Grove.Display.Templates.Services;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

/// <summary>The retired LiquidPart's bodies as DisplayTreeFieldSet trees (docs/MVC/shape-engine.md › Component coverage).</summary>
public sealed class LiquidBodiesTests
{
    [Fact]
    public void A_body_rendering_a_shape_by_name_becomes_that_component()
    {
        var conversion = LiquidBodies.Convert("{{ \"CounterButton\" | shape_new | shape_render }}", "item1");

        Assert.Equal("CounterButton", conversion.Tree["component"]?.ToString());
        Assert.Null(conversion.CompositeName);
    }

    [Fact]
    public void A_body_without_liquid_becomes_rich_text()
    {
        var conversion = LiquidBodies.Convert("<p>Hello</p>", "item1");

        Assert.Equal("RichText", conversion.Tree["component"]?.ToString());
        Assert.Equal("<p>Hello</p>", conversion.Tree["properties"]?["Html"]?.ToString());
        Assert.Null(conversion.CompositeName);
    }

    [Fact]
    public void A_body_with_liquid_becomes_a_composite_of_its_own_reading_its_item()
    {
        var conversion = LiquidBodies.Convert("<h1>{{ Model.ContentItem.DisplayText }}</h1>", "item1");

        Assert.Equal(LiquidBodies.CompositeName("item1"), conversion.CompositeName);
        Assert.Equal(conversion.CompositeName, conversion.Tree["component"]?.ToString());
        Assert.Equal("item1", conversion.Tree["properties"]?["ContentItemId"]?.ToString());
        Assert.Equal("<h1>{{ Content.ContentItemId[Model.ContentItemId].DisplayText }}</h1>", conversion.CompositeSource);
    }

    [Fact]
    public void An_empty_body_becomes_empty_rich_text()
    {
        var conversion = LiquidBodies.Convert(null, "item1");

        Assert.Equal("RichText", conversion.Tree["component"]?.ToString());
        Assert.IsType<JsonObject>(conversion.Tree["properties"]);
    }

    [Fact]
    public void A_body_s_text_includes_its_properties_zones_and_composite_sources()
    {
        var tree = JsonNode.Parse("""
            { "component": "Stack", "zones": { "Content": [
                { "component": "RichText", "properties": { "Html": "<p>Hello</p>" } },
                { "component": "LiquidBody_item1", "properties": { "ContentItemId": "item1" } } ] } }
            """);

        var text = DisplayTreeText.Extract(tree, new Dictionary<string, string> { ["LiquidBody_item1"] = "<h1>{{ Model.Title }}</h1> World {% if x %}!{% endif %}" });

        Assert.Contains("<p>Hello</p>", text);
        Assert.Contains("World", text);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("{%", text);
    }
}
