using System.Collections.Frozen;

namespace Grove.Communications.Email.Services;

/// <summary>The receive registry of the e-mail channel: every receive provider by technical name.</summary>
public class EmailReceiveProviderOptions
{
    private readonly Dictionary<string, EmailReceiveProviderTypeOptions> _providers = [];

    private FrozenDictionary<string, EmailReceiveProviderTypeOptions> _readonlyProviders;

    public IReadOnlyDictionary<string, EmailReceiveProviderTypeOptions> Providers
        => _readonlyProviders ??= _providers.ToFrozenDictionary(x => x.Key, x => x.Value);

    /// <summary>Adds a provider if none of that name exists.</summary>
    public EmailReceiveProviderOptions TryAddProvider(string name, EmailReceiveProviderTypeOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_providers.TryAdd(name, options))
        {
            _readonlyProviders = null;
        }

        return this;
    }

    public EmailReceiveProviderOptions RemoveProvider(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (_providers.Remove(name))
        {
            _readonlyProviders = null;
        }

        return this;
    }
}

public class EmailReceiveProviderTypeOptions
{
    public EmailReceiveProviderTypeOptions(Type type)
    {
        if (!typeof(IEmailReceiveProvider).IsAssignableFrom(type))
        {
            throw new ArgumentException($"The type must implement the '{nameof(IEmailReceiveProvider)}' interface.");
        }

        Type = type;
    }

    public Type Type { get; }

    public bool IsEnabled { get; set; }
}
