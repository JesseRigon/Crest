using Grove.Contents.Management;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Fields;

/// <summary>
/// The field type registry (docs/content-engine.md › Fields and field types): every field type is
/// registered here by the module that owns its domain, with the settings it is configured by; its
/// display driver and handler chain on the returned builder. The type editor offers exactly what
/// is registered.
/// </summary>
public sealed class ContentFieldTypeOptions
{
    private readonly Dictionary<string, ContentFieldTypeRegistration> _types = new(StringComparer.Ordinal);

    /// <summary>The registered field types, by name.</summary>
    public IReadOnlyCollection<ContentFieldTypeRegistration> Types => _types.Values;

    public bool IsRegistered(string fieldType) => _types.ContainsKey(fieldType);

    internal void Register(ContentFieldTypeRegistration registration) => _types[registration.Name] = registration;
}

/// <summary>A registered field type: its name, its class, its settings class (null when it has none of its own) and the module that declares it.</summary>
public sealed record ContentFieldTypeRegistration(string Name, Type FieldType, Type SettingsType, string Module);

public static class ContentFieldTypeRegistryExtensions
{
    /// <summary>
    /// Registers a field type with its settings, from the module that owns its domain (the calling
    /// assembly). Chain its display driver and handler on the returned builder.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static ContentFieldOptionBuilder AddFieldType<TField, TSettings>(this IServiceCollection services)
        where TField : ContentField
        where TSettings : Grove.Contents.Management.Metadata.Settings.FieldSettings =>
        services.AddFieldType(typeof(TField), typeof(TSettings), System.Reflection.Assembly.GetCallingAssembly().GetName().Name);

    /// <summary>Registers a field type configured by the common field settings only.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static ContentFieldOptionBuilder AddFieldType<TField>(this IServiceCollection services)
        where TField : ContentField =>
        services.AddFieldType(typeof(TField), null, System.Reflection.Assembly.GetCallingAssembly().GetName().Name);

    public static ContentFieldOptionBuilder AddFieldType(this IServiceCollection services, Type fieldType, Type settingsType, string module)
    {
        services.Configure<ContentFieldTypeOptions>(options => options.Register(new ContentFieldTypeRegistration(fieldType.Name, fieldType, settingsType, module)));
        return services.AddContentField(fieldType);
    }
}
