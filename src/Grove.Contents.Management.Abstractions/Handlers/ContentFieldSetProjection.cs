namespace Grove.Contents.Management.Handlers;

/// <summary>
/// A read projection of a field set (docs/content-engine.md › Behaviour is workflows): a value an
/// item yields when it is read, not stored and not computed by a flow (its route values, its body,
/// its preview URL, its culture). The field set's module registers it
/// (<see cref="ContentFieldSetProjectionExtensions.AddContentFieldSetProjection{TProjection}"/>);
/// the content manager asks it for the aspect being populated whenever an item carries the field set.
/// </summary>
public interface IContentFieldSetProjection
{
    /// <summary>The field set's stored name (its definition's name).</summary>
    string FieldSet { get; }

    Task ProjectAsync(ContentItemAspectContext context, ContentFieldSet fieldSet);
}

/// <inheritdoc cref="IContentFieldSetProjection"/>
public abstract class ContentFieldSetProjection<TFieldSet> : IContentFieldSetProjection where TFieldSet : ContentFieldSet
{
    public string FieldSet => ContentFieldSetName.Of<TFieldSet>();

    Task IContentFieldSetProjection.ProjectAsync(ContentItemAspectContext context, ContentFieldSet fieldSet) =>
        fieldSet is TFieldSet typed ? ProjectAsync(context, typed) : Task.CompletedTask;

    protected abstract Task ProjectAsync(ContentItemAspectContext context, TFieldSet fieldSet);
}

public static class ContentFieldSetProjectionExtensions
{
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddContentProjection<TProjection>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        where TProjection : class, IContentProjection
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<IContentProjection, TProjection>(services);
        return services;
    }

    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddContentFieldSetProjection<TProjection>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        where TProjection : class, IContentFieldSetProjection
    {
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<IContentFieldSetProjection, TProjection>(services);
        return services;
    }
}

/// <summary>
/// A read projection of an item as a whole (docs/content-engine.md › Behaviour is workflows): an
/// aspect a module answers for any item it applies to (a contained item's route values, the default
/// route of an item without one). Asked whenever the aspect is populated, after the field sets'.
/// </summary>
public interface IContentProjection
{
    Task ProjectAsync(ContentItemAspectContext context);
}
