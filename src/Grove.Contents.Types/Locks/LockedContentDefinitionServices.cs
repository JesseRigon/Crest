#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Types;

/// <summary>
/// The content-types admin obeys the definition locks through this decorator: structure and
/// settings changes go through <see cref="IContentDefinitionService"/>, a tenant surface; module
/// code (migrations) never touches it, so wrapping it enforces the locks without ever getting
/// in a migration's way.
/// </summary>
public static class LockedContentDefinitionServices
{
    /// <summary>Wraps the current registrations; a no-op when the content-types module has not registered them.</summary>
    public static IServiceCollection AddDefinitionLocks(this IServiceCollection services)
    {
        services.AddScoped<DefinitionLockGuard>();
        Decorate<IContentDefinitionService>(services, (inner, sp) => new LockedContentDefinitionService(inner, sp.GetRequiredService<DefinitionLockGuard>()));
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

public sealed class LockedContentDefinitionService(IContentDefinitionService inner, DefinitionLockGuard guard) : IContentDefinitionService
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
        await guard.EnsureTypePartChangeAsync(typeName, partName, DefinitionChanges.Detached);
        await inner.RemovePartFromTypeAsync(partName, typeName);
    }

    public Task<ContentPartDefinition> AddPartAsync(string name) => inner.AddPartAsync(name);

    public async Task RemovePartAsync(string name)
    {
        await guard.EnsurePartChangeAsync(name, DefinitionChanges.Deleted);
        await inner.RemovePartAsync(name);
    }

    public Task AddFieldToPartAsync(string fieldName, string fieldTypeName, string partName) => inner.AddFieldToPartAsync(fieldName, fieldTypeName, partName);

    public Task AddFieldToPartAsync(string fieldName, string displayName, string fieldTypeName, string partName) =>
        inner.AddFieldToPartAsync(fieldName, displayName, fieldTypeName, partName);

    public async Task RemoveFieldFromPartAsync(string fieldName, string partName)
    {
        await guard.EnsureFieldChangeAsync(partName, fieldName, DefinitionChanges.Removed);
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
