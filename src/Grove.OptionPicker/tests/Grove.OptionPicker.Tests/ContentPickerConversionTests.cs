using System.Text.Json.Nodes;
using Fluid;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Fields;
using Grove.Models;
using Grove.OptionPicker.ContentItems;
using Grove.Settings;
using Xunit;

namespace Grove.OptionPicker.Tests;

// The retired content picker field converts into an option picker over the content items source
// (docs/server.md § 3): definitions, stored values, contained items, old recipes, and Liquid reads
// the converted value.
public class ContentPickerConversionTests
{
    [Fact]
    public void A_type_definition_converts_with_equivalent_settings()
    {
        var settings = new JsonObject
        {
            ["ContentPartFieldSettings"] = new JsonObject { ["DisplayName"] = "Customer", ["Position"] = "3", ["Editor"] = "Lucene" },
            ["ContentPickerFieldSettings"] = new JsonObject
            {
                ["Multiple"] = true,
                ["Required"] = true,
                ["Hint"] = "Who buys",
                ["Placeholder"] = "Pick one",
                ["DisplayedContentTypes"] = new JsonArray("Customer", "Vendor"),
                ["DisplayedStereotypes"] = new JsonArray("Party"),
                ["TitlePattern"] = "{{ Model.ContentItem.DisplayText | upcase }}",
            },
            ["ContentPickerFieldLuceneEditorSettings"] = new JsonObject { ["Index"] = "x" },
            ["GroveFieldVisibilitySettings"] = new JsonObject { ["Path"] = "Kind" },
        };

        var converted = ContentPickerConversion.ConvertSettings(settings);
        var picker = converted["OptionPickerFieldSettings"]!;

        Assert.Equal("contentitem:Customer,Vendor,@Party", picker["SourceKey"]!.GetValue<string>());
        Assert.True(picker["Multiple"]!.GetValue<bool>());
        Assert.True(picker["Required"]!.GetValue<bool>());
        Assert.Equal("Who buys", picker["Hint"]!.GetValue<string>());
        Assert.Equal("Pick one", picker["Placeholder"]!.GetValue<string>());
        Assert.Equal("{{ Model.ContentItem.DisplayText | upcase }}", picker["TitlePattern"]!.GetValue<string>());
        Assert.Null(converted["ContentPickerFieldSettings"]);
        Assert.Null(converted["ContentPickerFieldLuceneEditorSettings"]);
        Assert.Null(converted["ContentPartFieldSettings"]!["Editor"]);
        Assert.Equal("3", converted["ContentPartFieldSettings"]!["Position"]!.GetValue<string>());
        Assert.NotNull(converted["GroveFieldVisibilitySettings"]);
    }

    [Fact]
    public void All_types_and_the_default_title_pattern_convert_to_the_all_types_source_and_no_pattern()
    {
        var converted = ContentPickerConversion.ConvertSettings(new JsonObject
        {
            ["ContentPickerFieldSettings"] = new JsonObject
            {
                ["DisplayAllContentTypes"] = true,
                ["DisplayedContentTypes"] = new JsonArray("Ignored"),
                ["TitlePattern"] = ContentPickerConversion.DefaultTitlePattern,
            },
        });

        Assert.Equal("contentitem:*", converted["OptionPickerFieldSettings"]!["SourceKey"]!.GetValue<string>());
        Assert.Null(converted["OptionPickerFieldSettings"]!["TitlePattern"]);
    }

    [Fact]
    public void Stored_values_move_to_selected_ids_with_the_source_key_in_items_and_contained_items()
    {
        var types = new Dictionary<string, ContentTypeDefinition>
        {
            ["Invoice"] = Type("Invoice", "Customer"),
            ["InvoiceLine"] = Type("InvoiceLine", "Item"),
        };
        var content = new JsonObject
        {
            ["Invoice"] = new JsonObject { ["Customer"] = new JsonObject { ["ContentItemIds"] = new JsonArray("c1") } },
            ["Lines"] = new JsonObject
            {
                ["ContentItems"] = new JsonArray(new JsonObject
                {
                    ["ContentItemId"] = "l1",
                    ["ContentType"] = "InvoiceLine",
                    ["InvoiceLine"] = new JsonObject { ["Item"] = new JsonObject { ["ContentItemIds"] = new JsonArray("i1", "i2") } },
                }),
            },
        };

        Assert.True(ContentPickerConversion.ConvertItem(content, "Invoice", name => types.GetValueOrDefault(name)));

        var customer = content["Invoice"]!["Customer"]!;
        Assert.Null(customer["ContentItemIds"]);
        Assert.Equal(["c1"], customer["SelectedIds"]!.AsArray().Select(id => id!.GetValue<string>()));
        Assert.Equal("contentitem:Customer", customer["SourceKey"]!.GetValue<string>());

        var item = content["Lines"]!["ContentItems"]![0]!["InvoiceLine"]!["Item"]!;
        Assert.Equal(["i1", "i2"], item["SelectedIds"]!.AsArray().Select(id => id!.GetValue<string>()));
        Assert.Equal("contentitem:Item", item["SourceKey"]!.GetValue<string>());

        // Converting again changes nothing.
        Assert.False(ContentPickerConversion.ConvertItem(content, "Invoice", name => types.GetValueOrDefault(name)));
    }

    [Fact]
    public void An_old_recipe_converts_on_the_way_in()
    {
        var definitions = JsonNode.Parse("""
            {
              "name": "ContentDefinition",
              "ContentParts": [
                {
                  "Name": "Invoice",
                  "ContentPartFieldDefinitionRecords": [
                    { "FieldName": "ContentPickerField", "Name": "Customer", "Settings": { "ContentPickerFieldSettings": { "DisplayedContentTypes": [ "Customer" ] } } },
                    { "FieldName": "TextField", "Name": "Number", "Settings": {} }
                  ]
                }
              ]
            }
            """)!.AsObject();

        Assert.True(ContentPickerConversion.ConvertDefinitionStep(definitions));
        var fields = definitions["ContentParts"]![0]!["ContentPartFieldDefinitionRecords"]!;
        Assert.Equal("OptionPickerField", fields[0]!["FieldName"]!.GetValue<string>());
        Assert.Equal("contentitem:Customer", fields[0]!["Settings"]!["OptionPickerFieldSettings"]!["SourceKey"]!.GetValue<string>());
        Assert.Equal("TextField", fields[1]!["FieldName"]!.GetValue<string>());

        var data = JsonNode.Parse("""
            [ { "ContentItemId": "x", "ContentType": "Invoice", "Invoice": { "Customer": { "ContentItemIds": [ "c1" ] }, "Number": { "Text": "1" } } } ]
            """);
        Assert.True(ContentPickerConversion.ConvertValues(data));
        Assert.Equal("c1", data![0]!["Invoice"]!["Customer"]!["SelectedIds"]![0]!.GetValue<string>());
        Assert.Null(data[0]!["Invoice"]!["Customer"]!["ContentItemIds"]);
    }

    [Fact]
    public async Task Liquid_reads_a_converted_field()
    {
        var content = new JsonObject { ["Invoice"] = new JsonObject { ["Customer"] = new JsonObject { ["ContentItemIds"] = new JsonArray("c1", "c2") } } };
        ContentPickerConversion.ConvertItem(content, "Invoice", _ => Type("Invoice", "Customer"));

        var item = new ContentItem { ContentType = "Invoice" };
        item.Merge(content);
        var field = item.Get<ContentPart>("Invoice")!.Get<OptionPickerField>("Customer")!;

        var options = new TemplateOptions();
        options.MemberAccessStrategy.Register<OptionPickerField>();
        var template = new FluidParser().Parse("{{ Field.SelectedIds | join: ',' }} from {{ Field.SourceKey }}");
        var output = await template.RenderAsync(new TemplateContext(new { Field = field }, options));

        Assert.Equal("c1,c2 from contentitem:Customer", output);
    }

    private static ContentTypeDefinition Type(string name, string pickerField)
    {
        var settings = new JsonObject
        {
            [nameof(OptionPickerFieldSettings)] = new JsonObject { ["SourceKey"] = OptionSourceKeys.ForContentItems([pickerField]) },
        };
        var part = new ContentPartDefinition(name, [new ContentPartFieldDefinition(new ContentFieldDefinition(ContentPickerConversion.FieldType), pickerField, settings)], new JsonObject());
        return new ContentTypeDefinition(name, name, [new ContentTypePartDefinition(name, part, new JsonObject())], new JsonObject());
    }
}
