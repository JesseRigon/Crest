using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Management.Handlers;

namespace Grove.Contents.Management;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a field set type. This method may be called multiple times safely,
    /// to reconfigure an existing part.
    /// </summary>
    public static ContentFieldSetOptionBuilder AddContentFieldSet<TContentFieldSet>(this IServiceCollection services)
        where TContentFieldSet : ContentFieldSet
    {
        return services.AddContentFieldSet(typeof(TContentFieldSet));
    }

    /// <summary>
    /// Registers a field set type. This method may be called multiple times safely,
    /// to reconfigure an existing part.
    /// </summary>
    public static ContentFieldSetOptionBuilder AddContentFieldSet(this IServiceCollection services, Type contentFieldSetType)
    {
        services.Configure<ContentOptions>(o => o.GetOrAddContentFieldSet(contentFieldSetType));
        return new ContentFieldSetOptionBuilder(services, contentFieldSetType);
    }

    /// <summary>
    /// Registers a content field type. This method may be called multiple times safely,
    /// to reconfigure an existing field.
    /// </summary>
    public static ContentFieldOptionBuilder AddContentField<TContentField>(this IServiceCollection services)
        where TContentField : ContentField
    {
        return services.AddContentField(typeof(TContentField));
    }

    /// <summary>
    /// Registers a content field type. This method may be called multiple times safely,
    /// to reconfigure an existing field.
    /// </summary>
    public static ContentFieldOptionBuilder AddContentField(this IServiceCollection services, Type contentFieldType)
    {
        services.Configure<ContentOptions>(o => o.GetOrAddContentField(contentFieldType));
        return new ContentFieldOptionBuilder(services, contentFieldType);
    }

    /// <summary>
    /// Register a handler for use with a field set.
    /// </summary>
    /// <typeparam name="TContentFieldSetHandler"></typeparam>
    public static ContentFieldSetOptionBuilder AddHandler<TContentFieldSetHandler>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetHandler : class, IContentFieldSetHandler
    {
        return builder.AddHandler(typeof(TContentFieldSetHandler));
    }

    /// <summary>
    /// Register a handler for use with a field set.
    /// </summary>
    public static ContentFieldSetOptionBuilder AddHandler(this ContentFieldSetOptionBuilder builder, Type handlerType)
    {
        builder.Services.TryAddScoped(handlerType);
        builder.Services.Configure<ContentOptions>(o =>
        {
            o.AddFieldSetHandler(builder.ContentFieldSetType, handlerType);
        });

        return builder;
    }

    /// <summary>
    /// Remove a handler registration from a field set.
    /// </summary>
    /// <typeparam name="TContentFieldSetHandler"></typeparam>
    public static ContentFieldSetOptionBuilder RemoveHandler<TContentFieldSetHandler>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetHandler : class, IContentFieldSetHandler
    {
        return builder.RemoveHandler(typeof(TContentFieldSetHandler));
    }

    /// <summary>
    /// Remove a handler registration from a field set.
    /// </summary>
    public static ContentFieldSetOptionBuilder RemoveHandler(this ContentFieldSetOptionBuilder builder, Type handlerType)
    {
        builder.Services.RemoveAll(handlerType);
        builder.Services.Configure<ContentOptions>(o =>
        {
            o.RemoveFieldSetHandler(builder.ContentFieldSetType, handlerType);
        });

        return builder;
    }

    /// <summary>
    /// Register a handler for use with a content field.
    /// </summary>
    /// <typeparam name="TContentFieldHandler"></typeparam>
    public static ContentFieldOptionBuilder AddHandler<TContentFieldHandler>(this ContentFieldOptionBuilder builder)
        where TContentFieldHandler : class, IContentFieldHandler
    {
        return builder.AddHandler(typeof(TContentFieldHandler));
    }

    /// <summary>
    /// Register a handler for use with a content field.
    /// </summary>
    public static ContentFieldOptionBuilder AddHandler(this ContentFieldOptionBuilder builder, Type handlerType)
    {
        builder.Services.TryAddScoped(handlerType);
        builder.Services.Configure<ContentOptions>(o =>
        {
            o.AddFieldHandler(builder.ContentFieldType, handlerType);
        });

        return builder;
    }

    /// <summary>
    /// Remove a handler registration from a content field.
    /// </summary>
    /// <typeparam name="TContentFieldHandler"></typeparam>
    public static ContentFieldOptionBuilder RemoveHandler<TContentFieldHandler>(this ContentFieldOptionBuilder builder)
        where TContentFieldHandler : class, IContentFieldHandler
    {
        return builder.RemoveHandler(typeof(TContentFieldHandler));
    }

    /// <summary>
    /// Remove a handler registration from a content field.
    /// </summary>
    public static ContentFieldOptionBuilder RemoveHandler(this ContentFieldOptionBuilder builder, Type handlerType)
    {
        builder.Services.RemoveAll(handlerType);
        builder.Services.Configure<ContentOptions>(o =>
        {
            o.RemoveFieldHandler(builder.ContentFieldType, handlerType);
        });

        return builder;
    }
}
