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

    public Task AddFieldSetToTypeAsync(string fieldSetName, string typeName) => inner.AddFieldSetToTypeAsync(fieldSetName, typeName);

    public Task AddReusableFieldSetToTypeAsync(string name, string displayName, string description, string fieldSetName, string typeName) =>
        inner.AddReusableFieldSetToTypeAsync(name, displayName, description, fieldSetName, typeName);

    public async Task RemoveFieldSetFromTypeAsync(string fieldSetName, string typeName)
    {
        await guard.EnsureTypeFieldSetChangeAsync(typeName, fieldSetName, DefinitionChanges.Detached);
        await inner.RemoveFieldSetFromTypeAsync(fieldSetName, typeName);
    }

    public Task<ContentFieldSetDefinition> AddFieldSetAsync(string name) => inner.AddFieldSetAsync(name);

    public async Task RemoveFieldSetAsync(string name)
    {
        await guard.EnsureFieldSetChangeAsync(name, DefinitionChanges.Deleted);
        await inner.RemoveFieldSetAsync(name);
    }

    public Task AddFieldToFieldSetAsync(string fieldName, string fieldTypeName, string fieldSetName) => inner.AddFieldToFieldSetAsync(fieldName, fieldTypeName, fieldSetName);

    public Task AddFieldToFieldSetAsync(string fieldName, string displayName, string fieldTypeName, string fieldSetName) =>
        inner.AddFieldToFieldSetAsync(fieldName, displayName, fieldTypeName, fieldSetName);

    public async Task RemoveFieldFromFieldSetAsync(string fieldName, string fieldSetName)
    {
        await guard.EnsureFieldChangeAsync(fieldSetName, fieldName, DefinitionChanges.Removed);
        await inner.RemoveFieldFromFieldSetAsync(fieldName, fieldSetName);
    }

    // Display surface only (display name, editor, display mode): a lock leaves it editable.
    public Task AlterFieldAsync(AlterFieldContext context) => inner.AlterFieldAsync(context);
    public Task AlterTypeFieldSetAsync(AlterTypeFieldSetContext context) => inner.AlterTypeFieldSetAsync(context);
    public Task AlterTypeFieldSetsOrderAsync(ContentTypeDefinition typeDefinition, string[] fieldSetNames) => inner.AlterTypeFieldSetsOrderAsync(typeDefinition, fieldSetNames);
    public Task AlterFieldSetFieldsOrderAsync(ContentFieldSetDefinition fieldSetDefinition, string[] fieldNames) => inner.AlterFieldSetFieldsOrderAsync(fieldSetDefinition, fieldNames);
    public Task<string> GenerateContentTypeNameFromDisplayNameAsync(string displayName) => inner.GenerateContentTypeNameFromDisplayNameAsync(displayName);
    public Task<string> GenerateFieldNameFromDisplayNameAsync(string fieldSetName, string displayName) => inner.GenerateFieldNameFromDisplayNameAsync(fieldSetName, displayName);
}
