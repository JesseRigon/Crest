using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Builders;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Utilities;

namespace Grove.Contents.Management.Metadata;

public static class ContentDefinitionManagerExtensions
{
    public static async Task AlterTypeDefinitionAsync(this IContentDefinitionManager manager, string name, Func<ContentTypeDefinitionBuilder, Task> alterationAsync)
    {
        var typeDefinition = (await manager.LoadTypeDefinitionAsync(name)) ?? new ContentTypeDefinition(name, name.CamelFriendly());
        var builder = new ContentTypeDefinitionBuilder(typeDefinition);
        await alterationAsync(builder);
        await manager.StoreTypeDefinitionAsync(builder.Build());
    }

    public static async Task AlterTypeDefinitionAsync(this IContentDefinitionManager manager, string name, Action<ContentTypeDefinitionBuilder> alteration)
    {
        var typeDefinition = (await manager.LoadTypeDefinitionAsync(name)) ?? new ContentTypeDefinition(name, name.CamelFriendly());
        var builder = new ContentTypeDefinitionBuilder(typeDefinition);
        alteration(builder);
        await manager.StoreTypeDefinitionAsync(builder.Build());
    }

    public static async Task AlterFieldSetDefinitionAsync(this IContentDefinitionManager manager, string name, Func<ContentFieldSetDefinitionBuilder, Task> alterationAsync)
    {
        var partDefinition = (await manager.LoadFieldSetDefinitionAsync(name)) ?? new ContentFieldSetDefinition(name);
        var builder = new ContentFieldSetDefinitionBuilder(partDefinition);
        await alterationAsync(builder);
        await manager.StoreFieldSetDefinitionAsync(builder.Build());
    }

    public static async Task AlterFieldSetDefinitionAsync(this IContentDefinitionManager manager, string name, Action<ContentFieldSetDefinitionBuilder> alteration)
    {
        var partDefinition = (await manager.LoadFieldSetDefinitionAsync(name)) ?? new ContentFieldSetDefinition(name);
        var builder = new ContentFieldSetDefinitionBuilder(partDefinition);
        alteration(builder);
        await manager.StoreFieldSetDefinitionAsync(builder.Build());
    }

    /// <summary>
    /// Migrate existing ContentFieldSet settings to WithSettings<typeparamref name="TSettings"/>
    /// This method will be removed in a future release.
    /// </summary>
    /// <typeparam name="TFieldSet"></typeparam>
    /// <typeparam name="TSettings"></typeparam>
    /// <param name="manager"></param>
    public static async Task MigrateFieldSetSettingsAsync<TFieldSet, TSettings>(this IContentDefinitionManager manager)
        where TFieldSet : ContentFieldSet where TSettings : class
    {
        var contentTypes = await manager.LoadTypeDefinitionsAsync();

        foreach (var contentType in contentTypes)
        {
            var partDefinition = contentType.FieldSets.FirstOrDefault(x => x.FieldSetDefinition.Name == ContentFieldSetName.Of<TFieldSet>());
            if (partDefinition != null)
            {
                var existingSettings = partDefinition.Settings.ToObject<TSettings>();

                // Remove existing properties from JObject
                var properties = typeof(TSettings).GetProperties();
                foreach (var property in properties)
                {
                    partDefinition.Settings.Remove(property.Name);
                }

                // Apply existing settings to type definition WithSettings<T>
                await manager.AlterTypeDefinitionAsync(contentType.Name, typeBuilder =>
                {
                    typeBuilder.WithFieldSet(partDefinition.Name, partBuilder =>
                    {
                        partBuilder.WithSettings(existingSettings);
                    });

                    return Task.CompletedTask;
                });
            }
        }
    }

    /// <summary>
    /// Migrate existing ContentField settings to WithSettings<typeparamref name="TSettings"/>
    /// This method will be removed in a future release.
    /// </summary>
    /// <typeparam name="TField"></typeparam>
    /// <typeparam name="TSettings"></typeparam>
    /// <param name="manager"></param>
    public static async Task MigrateFieldSettingsAsync<TField, TSettings>(this IContentDefinitionManager manager)
        where TField : ContentField where TSettings : class
    {
        var partDefinitions = await manager.LoadFieldSetDefinitionsAsync();

        foreach (var partDefinition in partDefinitions)
        {
            await manager.AlterFieldSetDefinitionAsync(partDefinition.Name, partBuilder =>
            {
                foreach (var fieldDefinition in partDefinition.Fields.Where(x => x.FieldTypeDefinition.Name == typeof(TField).Name))
                {
                    var existingFieldSettings = fieldDefinition.Settings.ToObject<TSettings>();

                    // Do this before creating builder, so settings are removed from the builder settings object.
                    // Remove existing properties from JObject
                    var fieldSettingsProperties = existingFieldSettings.GetType().GetProperties();
                    var hasSetting = false;
                    foreach (var property in fieldSettingsProperties)
                    {
                        if (fieldDefinition.Settings.ContainsKey(property.Name))
                        {
                            hasSetting = true;
                            fieldDefinition.Settings.Remove(property.Name);
                        }
                    }

                    // Only include settings if the definition already has at least one of these settings.
                    if (hasSetting)
                    {
                        partBuilder.WithField(fieldDefinition.Name, fieldBuilder =>
                        {
                            fieldBuilder.WithSettings(existingFieldSettings);
                        });
                    }
                }
            });
        }
    }
}
