using Crest.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentTypes;
using Crest.ContentTypes.Editors;
using Crest.DisplayManagement.ModelBinding;

namespace Crest.Services;

/// <summary>
/// The stock content-types admin obeys the definition locks through these two decorators:
/// structure changes go through <see cref="IContentDefinitionService"/>, settings editors
/// (Required, picker bindings, ...) through <see cref="IContentDefinitionDisplayManager"/>'s
/// Update* methods, which write straight to the definition manager. Both are tenant
/// surfaces; module code (migrations) never touches either, so wrapping them enforces the
/// locks without ever getting in a migration's way.
/// </summary>
public static class LockedContentDefinitionServices
{
    /// <summary>Wraps the current registrations; a no-op when the content-types module has not registered them.</summary>
    public static IServiceCollection AddCrestDefinitionLocks(this IServiceCollection services)
    {
        services.AddScoped<CrestDefinitionLockGuard>();
        Decorate<IContentDefinitionService>(services, (inner, sp) => new LockedContentDefinitionService(inner, sp.GetRequiredService<CrestDefinitionLockGuard>()));
        Decorate<IContentDefinitionDisplayManager>(services, (inner, sp) => new LockedContentDefinitionDisplayManager(inner, sp.GetRequiredService<CrestDefinitionLockGuard>()));
        return services;
    }

    private static void Decorate<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> decorate) where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(TService), sp => decorate(Resolve<TService>(descriptor, sp), sp), descriptor.Lifetime));
    }

    private static TService Resolve<TService>(ServiceDescriptor descriptor, IServiceProvider sp) where TService : class
    {
        if (descriptor.ImplementationInstance is TService instance) return instance;
        if (descriptor.ImplementationFactory is not null) return (TService)descriptor.ImplementationFactory(sp);
        return (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}

public sealed class LockedContentDefinitionService(IContentDefinitionService inner, CrestDefinitionLockGuard guard) : IContentDefinitionService
{
    public Task<ContentTypeDefinition> AddTypeAsync(string name, string displayName) => inner.AddTypeAsync(name, displayName);

    public async Task RemoveTypeAsync(string name, bool deleteContent)
    {
        await guard.EnsureTypeDeleteAsync(name);
        await inner.RemoveTypeAsync(name, deleteContent);
    }

    public Task AddPartToTypeAsync(string partName, string typeName) => inner.AddPartToTypeAsync(partName, typeName);

    public Task AddReusablePartToTypeAsync(string name, string displayName, string description, string partName, string typeName) =>
        inner.AddReusablePartToTypeAsync(name, displayName, description, partName, typeName);

    public async Task RemovePartFromTypeAsync(string partName, string typeName)
    {
        await guard.EnsureTypePartChangeAsync(typeName, partName, CrestDefinitionChanges.Detached);
        await inner.RemovePartFromTypeAsync(partName, typeName);
    }

    public Task<ContentPartDefinition> AddPartAsync(string name) => inner.AddPartAsync(name);

    public async Task RemovePartAsync(string name)
    {
        await guard.EnsurePartChangeAsync(name, CrestDefinitionChanges.Deleted);
        await inner.RemovePartAsync(name);
    }

    public Task AddFieldToPartAsync(string fieldName, string fieldTypeName, string partName) => inner.AddFieldToPartAsync(fieldName, fieldTypeName, partName);

    public Task AddFieldToPartAsync(string fieldName, string displayName, string fieldTypeName, string partName) =>
        inner.AddFieldToPartAsync(fieldName, displayName, fieldTypeName, partName);

    public async Task RemoveFieldFromPartAsync(string fieldName, string partName)
    {
        await guard.EnsureFieldChangeAsync(partName, fieldName, CrestDefinitionChanges.Removed);
        await inner.RemoveFieldFromPartAsync(fieldName, partName);
    }

    // Display surface only (display name, editor, display mode): a lock leaves it editable.
    public Task AlterFieldAsync(AlterFieldContext context) => inner.AlterFieldAsync(context);
    public Task AlterTypePartAsync(AlterTypePartContext context) => inner.AlterTypePartAsync(context);
    public Task AlterTypePartsOrderAsync(ContentTypeDefinition typeDefinition, string[] partNames) => inner.AlterTypePartsOrderAsync(typeDefinition, partNames);
    public Task AlterPartFieldsOrderAsync(ContentPartDefinition partDefinition, string[] fieldNames) => inner.AlterPartFieldsOrderAsync(partDefinition, fieldNames);
    public Task<string> GenerateContentTypeNameFromDisplayNameAsync(string displayName) => inner.GenerateContentTypeNameFromDisplayNameAsync(displayName);
    public Task<string> GenerateFieldNameFromDisplayNameAsync(string partName, string displayName) => inner.GenerateFieldNameFromDisplayNameAsync(partName, displayName);
}

/// <summary>
/// The stock settings editors write every settings section of a definition at once, so a
/// locked field's, part's or attachment's editor is refused as a whole; the type editor
/// (type-level settings: creatable, listable, ...) is not locked.
/// </summary>
public sealed class LockedContentDefinitionDisplayManager(IContentDefinitionDisplayManager inner, CrestDefinitionLockGuard guard) : IContentDefinitionDisplayManager
{
    public Task<dynamic> BuildTypeEditorAsync(ContentTypeDefinition definition, IUpdateModel updater, string groupId = "") => inner.BuildTypeEditorAsync(definition, updater, groupId);
    public Task<dynamic> UpdateTypeEditorAsync(ContentTypeDefinition definition, IUpdateModel updater, string groupId = "") => inner.UpdateTypeEditorAsync(definition, updater, groupId);
    public Task<dynamic> BuildPartEditorAsync(ContentPartDefinition definition, IUpdateModel updater, string groupId = "") => inner.BuildPartEditorAsync(definition, updater, groupId);

    public async Task<dynamic> UpdatePartEditorAsync(ContentPartDefinition definition, IUpdateModel updater, string groupId = "")
    {
        await guard.EnsurePartChangeAsync(definition.Name, CrestDefinitionChanges.SettingsChanged);
        return await inner.UpdatePartEditorAsync(definition, updater, groupId);
    }

    public Task<dynamic> BuildTypePartEditorAsync(ContentTypePartDefinition definition, IUpdateModel updater, string groupId = "") => inner.BuildTypePartEditorAsync(definition, updater, groupId);

    public async Task<dynamic> UpdateTypePartEditorAsync(ContentTypePartDefinition definition, IUpdateModel updater, string groupId = "")
    {
        await guard.EnsureTypePartChangeAsync(definition.ContentTypeDefinition.Name, definition.Name, CrestDefinitionChanges.SettingsChanged);
        return await inner.UpdateTypePartEditorAsync(definition, updater, groupId);
    }

    public Task<dynamic> BuildPartFieldEditorAsync(ContentPartFieldDefinition definition, IUpdateModel updater, string groupId = "") => inner.BuildPartFieldEditorAsync(definition, updater, groupId);

    public async Task<dynamic> UpdatePartFieldEditorAsync(ContentPartFieldDefinition definition, IUpdateModel updater, string groupId = "")
    {
        await guard.EnsureFieldChangeAsync(definition.PartDefinition.Name, definition.Name, CrestDefinitionChanges.SettingsChanged);
        return await inner.UpdatePartFieldEditorAsync(definition, updater, groupId);
    }
}
