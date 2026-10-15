using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Records;
using Grove.Data.Migration;
using YesSql;

namespace Grove.Contents.OptionPicker.ContentItems;

/// <summary>
/// Converts a tenant's retired content picker fields (<see cref="ContentPickerConversion"/>): every
/// part's content picker field becomes an option picker over the content items source with
/// equivalent settings, then every version of every item has its stored values moved from
/// <c>ContentItemIds</c> to <c>SelectedIds</c> with the source key. A tenant without content
/// picker fields converts nothing.
/// </summary>
public sealed class ContentPickerFieldMigrations(IContentDefinitionManager definitions, ISession session) : DataMigration
{
    private const int BatchSize = 200;

    public async Task<int> CreateAsync()
    {
        await ConvertAsync();
        return 1;
    }

    private async Task ConvertAsync()
    {
        var converted = false;
        foreach (var part in await definitions.ListPartDefinitionsAsync())
        {
            var fields = part.Fields.Where(field => field.FieldDefinition.Name == ContentPickerConversion.RetiredFieldType).ToArray();
            foreach (var field in fields)
            {
                var settings = ContentPickerConversion.ConvertSettings(field.Settings);
                await definitions.AlterPartDefinitionAsync(part.Name, builder => builder.RemoveField(field.Name));
                await definitions.AlterPartDefinitionAsync(part.Name, builder => builder
                    .WithField(field.Name, replacement => replacement
                        .OfType(ContentPickerConversion.FieldType)
                        .MergeSettings(settings)));
                converted = true;
            }
        }

        if (!converted)
        {
            return;
        }

        var types = (await definitions.ListTypeDefinitionsAsync()).ToDictionary(type => type.Name, StringComparer.Ordinal);
        ContentTypeDefinition? Lookup(string name) => types.GetValueOrDefault(name);

        for (var skip = 0; ; skip += BatchSize)
        {
            var items = (await session.Query<ContentItem, ContentItemIndex>()
                .OrderBy(index => index.DocumentId)
                .Skip(skip)
                .Take(BatchSize)
                .ListAsync()).ToArray();

            foreach (var item in items)
            {
                JsonObject content = item.Content;
                if (ContentPickerConversion.ConvertItem(content, item.ContentType, Lookup))
                {
                    await session.SaveAsync(item);
                }
            }

            if (items.Length < BatchSize)
            {
                break;
            }
        }
    }
}
