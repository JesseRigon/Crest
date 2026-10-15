using System.Text.Json.Nodes;
using Grove.Display.Components.Editors;
using Grove.Display.Components.Models;
using Grove.Display.Management.Components;
using Xunit;

namespace Grove.Display.Components.Tests;

// Editors as trees (docs/blazor-display.md § 7, rework step 5): one editing context for every
// layout of the editors, and the registry choosing each field's editor by what it edits.
public sealed class ContentEditorTests
{
    private static readonly GroveContentItemMetadata s_metadata = new(
        "Invoice",
        "Invoice",
        [
            new GroveContentPartMetadata("Invoice", "Invoice",
            [
                new GroveContentFieldMetadata("Number", "TextField", "Number", true, string.Empty, "1", null),
                new GroveContentFieldMetadata("Express", "BooleanField", "Express", false, string.Empty, "2", null),
                new GroveContentFieldMetadata("Courier", "TextField", "Courier", true, string.Empty, "3",
                    new JsonObject { ["GroveFieldVisibilitySettings"] = new JsonObject { ["Path"] = "Express", ["Operator"] = "Any" } }),
                new GroveContentFieldMetadata("Region", "TextField", "Region", false, string.Empty, "4",
                    new JsonObject { ["GroveFieldVisibilitySettings"] = new JsonObject { ["Path"] = "Invoice.Number", ["Operator"] = "In", ["Keys"] = new JsonArray("EU-1", "EU-2") } }),
            ], null),
        ]);

    [Fact]
    public void EditsWriteIntoTheContentAndAnnounceTheChange()
    {
        var context = new ContentEditorContext(new JsonObject(), s_metadata, http: null);
        var changes = 0;
        context.Changed += () => changes++;

        context.Set("Invoice", "Number", "Text", JsonValue.Create("EU-1"));
        context.Set("TitlePart", null, "Title", JsonValue.Create("Invoice EU-1"));

        Assert.Equal("EU-1", context.Content["Invoice"]!["Number"]!["Text"]!.GetValue<string>());
        Assert.Equal("Invoice EU-1", context.Content["TitlePart"]!["Title"]!.GetValue<string>());
        Assert.Equal(2, changes);
    }

    [Fact]
    public void RequiredFieldsAreCheckedOnlyWhileTheyAreVisible()
    {
        var context = new ContentEditorContext(new JsonObject(), s_metadata, http: null);

        Assert.False(context.Validate());
        Assert.True(context.Errors.ContainsKey("Invoice.Number"));
        Assert.False(context.Errors.ContainsKey("Invoice.Courier"));

        context.Set("Invoice", "Number", "Text", JsonValue.Create("EU-1"));
        context.Set("Invoice", "Express", "Value", JsonValue.Create("yes"));

        Assert.False(context.Validate());
        Assert.True(context.Errors.ContainsKey("Invoice.Courier"));
    }

    [Fact]
    public void AScalarParentDecidesVisibilityByItsValue()
    {
        var context = new ContentEditorContext(new JsonObject(), s_metadata, http: null);
        var region = context.Field("Invoice", "Region")!;

        Assert.False(context.IsFieldVisible("Invoice", region));

        context.Set("Invoice", "Number", "Text", JsonValue.Create("US-9"));
        Assert.False(context.IsFieldVisible("Invoice", region));

        context.Set("Invoice", "Number", "Text", JsonValue.Create("EU-2"));
        Assert.True(context.IsFieldVisible("Invoice", region));
    }

    [Fact]
    public void TheRegistryPicksTheEditorThatNamesTheTypeElseTheCatchAll()
    {
        var registry = new ComponentRegistry([ComponentCatalogs.ForAssembly(typeof(TextFieldEditor).Assembly)]);

        Assert.True(registry.TryGetEditor("TextField", out var text));
        Assert.Equal("TextFieldEditor", text.Name);
        Assert.True(registry.TryGetEditor("DateTimeField", out var date));
        Assert.Equal("DateFieldEditor", date.Name);
        Assert.True(registry.TryGetEditor("HtmlBodyPart", out var html));
        Assert.Equal("HtmlEditor", html.Name);
        Assert.True(registry.TryGetEditor("SomethingNew", out var fallback));
        Assert.Equal("JsonEditor", fallback.Name);

        // Editors take the part and field they edit; the rest comes from the context.
        Assert.NotNull(text.Parameter(nameof(FieldEditorBase.PartName)));
        Assert.NotNull(text.Parameter(nameof(FieldEditorBase.FieldName)));
    }

    [Fact]
    public void TheEditorRootPublishesTheContentAsAnOutput()
    {
        var registry = new ComponentRegistry([ComponentCatalogs.ForAssembly(typeof(ContentEditor).Assembly)]);

        Assert.True(registry.TryGet("ContentEditor", out var editor));
        Assert.Equal(["ContentChanged"], editor.Outputs.Select(output => output.Name));
        Assert.Equal(ComponentInstances.Single, editor.Instances);
    }
}
