using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Localization.Translations.Services;
using Grove.Data.Localization;

namespace Grove.Services;

/// <summary>
/// Resolves a menu caption against the tenant translation store for the request culture, with
/// hierarchical context fallback - the resolution Grove's sidebar and app manifest use in
/// place of a bare <c>IDataLocalizer</c> lookup.
/// </summary>
/// <remarks>
/// <para><b>No default context.</b> Contexts are strict namespaces, so a translation stored
/// under one context is invisible to every other, and a bare lookup falls back to the invariant
/// literal even when the request culture translates the same caption elsewhere. The resolver
/// instead walks outward: the exact menu context first, then its parents by stripping
/// <c>':'</c>-separated segments (<c>Admin Menus:Primary Navigation</c> → <c>Admin Menus</c>),
/// and finally the best entry for the caption anywhere in the culture - preferring contexts
/// under the menu root over unrelated ones (so a sibling menu's translation beats a content
/// type's), then the most common value, ordinally tie-broken for determinism. The invariant
/// literal renders only when the culture holds no translation of the caption at all. An
/// explicit entry in the exact context always wins, so pinning a caption in the Translations
/// editor overrides every fallback. Each step checks the specific culture before its parents
/// (es-ES, then es), mirroring the localizer's own culture fallback.</para>
///
/// <para>This also covers data captions (New › content type, content type and group pages),
/// which are resolved ownerless: a content type name stored under the "Content Types" context is
/// simply the best alternative for a caption no menu context translates.</para>
///
/// <para>Per-request: indexes are built once per instance (scoped) from the same documents the
/// localizer reads, and resolution itself is synchronous so the recursive item serialization
/// stays flat. Both dependencies are optional - with the data localization feature absent the
/// resolver leaves captions untouched, matching the previous null-localizer behavior.</para>
/// </remarks>
public sealed class MenuCaptionResolver(IServiceProvider serviceProvider)
{
    private static readonly string RootContext = Grove.Contents.AdminMenu.DataLocalizationContext.AdminMenu(null);

    // Most-specific culture first (es-ES, then es): caption -> its entries in that culture.
    private List<ILookup<string, (string Context, string Value)>>? _cultureIndexes;
    private List<Dictionary<string, List<(string Context, string Value)>>>? _poIndexes;
    // PO tier 0 provenance: node UniqueId -> the declaring provider classes' full type
    // names, recorded at sync time in Merge's value-authority order.
    private bool _loaded;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        var translationsManager = serviceProvider.GetService<TranslationsManager>();
        if (translationsManager is not null)
        {
            var document = await translationsManager.GetTranslationsDocumentAsync();
            var indexes = new List<ILookup<string, (string, string)>>();
            for (var culture = CultureInfo.CurrentUICulture;
                 !string.IsNullOrEmpty(culture.Name);
                 culture = culture.Parent)
            {
                if (document.Translations.TryGetValue(culture.Name, out var entries))
                {
                    indexes.Add(entries
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                        .ToLookup(
                            entry => entry.Key,
                            entry => (entry.Context, entry.Value),
                            StringComparer.OrdinalIgnoreCase));
                }
            }

            _cultureIndexes = indexes;
        }

        // The PO layer of the resolution hierarchy (store edit -> PO -> invariant
        // literal). The dictionaries are parsed once per shell and cached by the
        // manager, so this is index construction over cached data, not file IO.
        var localizationManager = serviceProvider.GetService<ILocalizationManager>();
        if (localizationManager is not null)
        {
            _poIndexes = PoTranslationLookup.BuildCultureChainIndexes(
                localizationManager, CultureInfo.CurrentUICulture);
        }
    }

    /// <param name="caption">The displayed caption - the same key upstream's admin looks up.</param>
    /// <param name="menuName">The item's owning admin menu, when the built item still has one.</param>
    /// <param name="sourceContext">The type that declared the caption (a page registry entry's
    /// <c>DeclaredBy</c>): the PO layer's first-choice <c>msgctxt</c>.</param>
    public string Resolve(string caption, string? menuName, string? sourceContext) => Resolve(caption, menuName, sourceContext, invariant: null);

    /// <param name="invariant">The caption's invariant literal (MenuItem.Text.Name) when the
    /// displayed caption may already be a PO translation of it. Upstream's Contents menu
    /// builds the New branch's captions through S[displayName], so a content type upstream
    /// ships a catalog string for ("Account" → "Cuenta") arrives translated; the tenant's own
    /// translation is stored under the invariant, so the store is consulted for both.</param>
    public string Resolve(string caption, string? menuName, string? sourceContext, string? invariant)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return caption;
        }

        var resolved = ResolveCore(caption, menuName, sourceContext, out var fromStore);
        if (!fromStore && !string.IsNullOrEmpty(invariant) && !string.Equals(invariant, caption, StringComparison.Ordinal))
        {
            var byInvariant = ResolveCore(invariant, menuName, sourceContext, out fromStore);
            if (fromStore)
            {
                return byInvariant;
            }
        }

        return resolved;
    }

    private string ResolveCore(string caption, string? menuName, string? sourceContext, out bool fromStore)
    {
        fromStore = false;

        if (_cultureIndexes is { Count: > 0 } indexes)
        {
            // Exact context, then its parents by stripping ':'-separated segments.
            var context = Grove.Contents.AdminMenu.DataLocalizationContext.AdminMenu(
                string.IsNullOrEmpty(menuName) ? null : menuName);
            while (true)
            {
                foreach (var index in indexes)
                {
                    foreach (var (entryContext, value) in index[caption])
                    {
                        if (string.Equals(entryContext, context, StringComparison.OrdinalIgnoreCase))
                        {
                            fromStore = true;
                            return value;
                        }
                    }
                }

                var separator = context.LastIndexOf(':');
                if (separator < 0)
                {
                    break;
                }

                context = context[..separator];
            }

            // The culture's best alternative, from the nearest culture that has any. An item
            // with an owning menu prefers menu contexts (another menu's translation of the
            // same caption over, say, a content type's). An OWNERLESS caption is the reverse:
            // nothing in Grove's admin is ownerless but the locked New branch, whose captions
            // are content type names - so a "Content Types" entry is about this very item,
            // while a menu-context entry is about some other item that shares the literal
            // (an Account type flagged into the Content menu seeds "Account" there).
            var ownerless = string.IsNullOrEmpty(menuName);
            foreach (var index in indexes)
            {
                var candidates = index[caption].ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                var preferred = candidates
                    .Where(candidate => candidate.Context.StartsWith(RootContext, StringComparison.OrdinalIgnoreCase) != ownerless)
                    .ToArray();

                fromStore = true;
                return (preferred.Length > 0 ? preferred : candidates)
                    .GroupBy(candidate => candidate.Value, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .First().Key;
            }
        }

        // The PO layer: shipped baseline below the store, so a caption no tenant entry
        // covers still renders its catalog translation, and deleting a store entry
        // reverts to the shipped value rather than the invariant literal (delete walks
        // down the hierarchy - store edit -> PO -> literal). Tier 0 is the item's own
        // recorded declarers (via its UniqueId), then *.AdminMenu contexts, then flat -
        // see PoTranslationLookup for the tiering.
        if (_poIndexes is { Count: > 0 } poIndexes)
        {
            List<string>? sourceContexts = string.IsNullOrEmpty(sourceContext) ? null : [sourceContext];

            var poValue = PoTranslationLookup.Resolve(
                poIndexes, caption, PoTranslationLookup.AdminMenuContextSuffix, sourceContexts);
            if (poValue is not null)
            {
                return poValue;
            }
        }

        return caption;
    }
}
