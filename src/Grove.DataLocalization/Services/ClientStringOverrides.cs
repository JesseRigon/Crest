#nullable enable

using System.Globalization;
using Grove.Localization;

namespace Grove.DataLocalization.Services;

/// <summary>The translation store's entries under the client strings context, for the client strings endpoint.</summary>
public sealed class ClientStringOverrides(TranslationsManager translationsManager) : IClientStringOverrides
{
    public async Task<IReadOnlyDictionary<string, string>> GetAsync(CultureInfo culture, string context)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var document = await translationsManager.GetTranslationsDocumentAsync();
        var chain = new List<string>();
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            chain.Add(current.Name);
        }

        chain.Reverse();
        foreach (var name in chain)
        {
            if (!document.Translations.TryGetValue(name, out var entries))
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    strings[entry.Key] = entry.Value;
                }
            }
        }

        return strings;
    }
}
