using System.Text.Json.Nodes;
using Grove.Contents.OptionPicker.ContentItems;
using Xunit;

namespace Grove.Contents.OptionPicker.Tests;

// The retired picker fields convert into option list fields on the way in (docs/content-engine.md ›
// Picking is one field type): a taxonomy field over its taxonomy's terms, a user picker over the
// users source limited to its roles, a localization set picker over its types' sets.
public class RetiredPickerConversionTests
{
    [Fact]
    public void A_taxonomy_field_picks_its_taxonomys_terms()
    {
        var converted = RetiredPickerConversion.ConvertSettings(RetiredPickerConversion.TaxonomyField, new JsonObject
        {
            ["ContentFieldSettings"] = new JsonObject { ["Editor"] = "Tags", ["DisplayMode"] = "Tags" },
            ["TaxonomyFieldSettings"] = new JsonObject { ["TaxonomyContentItemId"] = "tax-1", ["Unique"] = true, ["LeavesOnly"] = true },
            ["TaxonomyFieldTagsEditorSettings"] = new JsonObject { ["Open"] = true },
        });

        var picker = converted["OptionListFieldSettings"]!;
        Assert.Equal("embedded:Taxonomy.Terms", picker["SourceKey"]!.GetValue<string>());
        Assert.False(picker["Multiple"]!.GetValue<bool>());
        Assert.True(picker["AllowCreate"]!.GetValue<bool>());
        Assert.Equal(["Parent", "Leaf"], picker["Filters"]!.AsArray().Select(filter => filter!["Path"]!.GetValue<string>()));
        Assert.Equal("tax-1", picker["Filters"]![0]!["Value"]!.GetValue<string>());
        Assert.Null(converted["ContentFieldSettings"]!["Editor"]);
        Assert.Equal("Tags", converted["ContentFieldSettings"]!["DisplayMode"]!.GetValue<string>());
        Assert.Null(converted["TaxonomyFieldSettings"]);
    }

    [Fact]
    public void A_user_picker_limited_to_roles_filters_the_users_source()
    {
        var converted = RetiredPickerConversion.ConvertSettings(RetiredPickerConversion.UserPickerField, new JsonObject
        {
            ["UserPickerFieldSettings"] = new JsonObject { ["Multiple"] = true, ["DisplayAllUsers"] = false, ["DisplayedRoles"] = new JsonArray("Editor", "Author") },
        });

        var picker = converted["OptionListFieldSettings"]!;
        Assert.Equal("users", picker["SourceKey"]!.GetValue<string>());
        Assert.True(picker["Multiple"]!.GetValue<bool>());
        Assert.Equal("Editor,Author", picker["Filters"]![0]!["Value"]!.GetValue<string>());
    }

    [Fact]
    public void Stored_values_and_definitions_convert_in_a_recipe()
    {
        var step = new JsonObject
        {
            ["ContentFieldSets"] = new JsonArray(new JsonObject
            {
                ["ContentFieldDefinitionRecords"] = new JsonArray(new JsonObject
                {
                    ["FieldName"] = "LocalizationSetContentPickerField",
                    ["Settings"] = new JsonObject { ["LocalizationSetContentPickerFieldSettings"] = new JsonObject { ["DisplayedContentTypes"] = new JsonArray("Page") } },
                }),
            }),
        };
        var item = new JsonObject { ["Article"] = new JsonObject { ["Tags"] = new JsonObject { ["TermContentItemIds"] = new JsonArray("t1"), ["TaxonomyContentItemId"] = "tax-1", ["TagNames"] = new JsonArray("One") } } };

        Assert.True(RetiredPickerConversion.ConvertDefinitionStep(step));
        Assert.True(RetiredPickerConversion.ConvertValues(item));

        var field = step["ContentFieldSets"]![0]!["ContentFieldDefinitionRecords"]![0]!;
        Assert.Equal("OptionListField", field["FieldName"]!.GetValue<string>());
        Assert.Equal("localization-set:Page", field["Settings"]!["OptionListFieldSettings"]!["SourceKey"]!.GetValue<string>());
        var tags = item["Article"]!["Tags"]!.AsObject();
        Assert.Equal(["t1"], tags["SelectedIds"]!.AsArray().Select(id => id!.GetValue<string>()));
        Assert.False(tags.ContainsKey("TagNames"));
    }
}
