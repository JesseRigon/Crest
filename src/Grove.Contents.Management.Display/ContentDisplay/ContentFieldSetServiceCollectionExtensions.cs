using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grove.Contents.Management.Display.ContentDisplay;

public static class ContentFieldSetServiceCollectionExtensions
{
    /// <summary>
    /// Register a display driver for use with a field set and all display modes and editors.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder UseDisplayDriver<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.UseDisplayDriver(typeof(TContentFieldSetDisplayDriver));
    }

    /// <summary>
    /// Register a display driver for use with a field set and all display modes and editors.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder UseDisplayDriver(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType)
    {
        return builder.ForDisplayMode(fieldSetDisplayDriverType)
            .ForEditor(fieldSetDisplayDriverType);
    }

    /// <summary>
    /// Removes a display driver from all display modes and editors.
    /// </summary>
    public static ContentFieldSetOptionBuilder RemoveDisplayDriver<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.RemoveDisplayDriver(typeof(TContentFieldSetDisplayDriver));
    }

    /// <summary>
    /// Removes a display driver from all display modes and editors.
    /// </summary>
    public static ContentFieldSetOptionBuilder RemoveDisplayDriver(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType)
    {
        builder.Services.Configure<ContentDisplayOptions>(o =>
        {
            o.RemoveContentFieldSetDisplayDriver(builder.ContentFieldSetType, fieldSetDisplayDriverType);
        });

        builder.Services.RemoveAll(fieldSetDisplayDriverType);

        return builder;
    }

    /// <summary>
    /// Registers a display driver for use with all display modes.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForDisplayMode<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.ForDisplayMode(typeof(TContentFieldSetDisplayDriver));
    }

    /// <summary>
    /// Registers a display driver for use with a specific display mode.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForDisplayMode(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType)
    {
        return builder.ForDisplayMode(fieldSetDisplayDriverType, d => true);
    }

    /// <summary>
    /// Registers a display driver for use with a specific display mode.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForDisplayMode<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder, Func<string, bool> predicate)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.ForDisplayMode(typeof(TContentFieldSetDisplayDriver), predicate);
    }

    /// <summary>
    /// Registers a display driver for use with a specific display mode.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForDisplayMode(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType, Func<string, bool> predicate)
    {
        builder.Services.Configure<ContentDisplayOptions>(o =>
        {
            o.ForContentFieldSetDisplayMode(builder.ContentFieldSetType, fieldSetDisplayDriverType, predicate);
        });

        builder.Services.TryAddScoped(fieldSetDisplayDriverType);

        return builder;
    }

    /// <summary>
    /// Registers a display driver for use with all editors.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForEditor<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.ForEditor(typeof(TContentFieldSetDisplayDriver));
    }

    /// <summary>
    /// Registers a display driver for use with a specific editor.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForEditor(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType)
    {
        return builder.ForEditor(fieldSetDisplayDriverType, d => true);
    }

    /// <summary>
    /// Registers a display driver for use with a specific editor.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForEditor<TContentFieldSetDisplayDriver>(this ContentFieldSetOptionBuilder builder, Func<string, bool> predicate)
        where TContentFieldSetDisplayDriver : class, IContentFieldSetDisplayDriver
    {
        return builder.ForEditor(typeof(TContentFieldSetDisplayDriver), predicate);
    }

    /// <summary>
    /// Registers a display driver for use with a specific editor.
    /// This method will override previous registrations for the driver type,
    /// and can be called multiple times safely, to reconfigure an existing driver.
    /// </summary>
    public static ContentFieldSetOptionBuilder ForEditor(this ContentFieldSetOptionBuilder builder, Type fieldSetDisplayDriverType, Func<string, bool> predicate)
    {
        builder.Services.Configure<ContentDisplayOptions>(o =>
        {
            o.ForContentFieldSetEditor(builder.ContentFieldSetType, fieldSetDisplayDriverType, predicate);
        });

        builder.Services.TryAddScoped(fieldSetDisplayDriverType);

        return builder;
    }
}
