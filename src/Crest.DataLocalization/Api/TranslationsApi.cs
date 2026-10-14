#nullable enable

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Api;
using Crest.DataLocalization.Models;
using Crest.DataLocalization.Services;
using Crest.Localization;
using Crest.Localization.Data;

namespace Crest.DataLocalization.Api;

public sealed class TranslationsApi : ApiContributor
{
    public override string Name => "Translations";

    public override string FeatureId => "Crest.DataLocalization";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("translations");
        routes.MapGet("", (HttpContext __http, [FromQuery] string? culture = null) => ApiHandlers.For<TranslationsHandlers>(__http).GetAsync(culture));
        routes.MapPut("", (HttpContext __http, [FromBody] TranslationsSaveModel model) => ApiHandlers.For<TranslationsHandlers>(__http).SaveAsync(model));
    }
}


/// <summary>
/// The Crest translations editor's API - the same functionality as Crest's stock
/// Translations page (Configuration → Localization → Translations), backed by the merge-correct
/// write discipline every other Crest writer already follows.
/// </summary>
/// <remarks>
/// The stock page's Save once replaced a culture's whole translation list with whatever its
/// editor enumerated (fixed in the platform 2026-10-10: it merges now). This API differs on
/// exactly the two points that
/// matter:
/// <list type="bullet">
/// <item>reads include ORPHANS - stored entries with no live descriptor - flagged as such, so
/// they are visible, editable and deletable instead of invisible;</item>
/// <item>the save merges: only the rows the client actually displayed are replaced (a blanked
/// displayed row deletes its entry), and every stored entry outside the displayed set is
/// carried over untouched.</item>
/// </list>
/// Per-culture authorization mirrors the stock page: ManageTranslations grants everything,
/// otherwise the per-culture dynamic permission decides edit rights, and
/// ViewDynamicTranslations grants read-only access.
/// </remarks>
public sealed class TranslationsHandlers(
    IAuthorizationService authorization,
    ILocalizationService localizationService,
    ILocalizationManager localizationManager,
    IEnumerable<ILocalizationDataProvider> localizationDataProviders,
    Crest.Routing.IPageRegistry pageRegistry,
    TranslationsManager translationsManager) : ApiHandlers
{
    public async Task<IResult> GetAsync(string? culture = null)
    {
        var cultures = await GetAllowedCulturesAsync();
        if (cultures.Length == 0)
        {
            return Forbid();
        }

        var selected = cultures.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, culture, StringComparison.OrdinalIgnoreCase)) ?? cultures[0];

        return Ok(new TranslationsModel(
            cultures,
            selected.Value,
            selected.CanEdit,
            await BuildGroupsAsync(selected.Value)));
    }

    public async Task<IResult> SaveAsync(TranslationsSaveModel model)
    {
        var cultures = await GetAllowedCulturesAsync();
        var target = cultures.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, model.Culture, StringComparison.OrdinalIgnoreCase));
        if (target is null || !target.CanEdit)
        {
            return Forbid();
        }

        // Merge, never replace-wholesale: the posted rows are the ones the editor DISPLAYED
        // (blank value = delete that entry); any stored entry outside the displayed set is
        // carried over untouched, so nothing the client never saw can be destroyed by saving.
        var displayed = model.Translations ?? [];
        var displayedKeys = displayed
            .Select(entry => MakeKey(entry.Context, entry.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var document = await translationsManager.GetTranslationsDocumentAsync();
        var carried = document.Translations.TryGetValue(target.Value, out var current)
            ? current.Where(entry => !displayedKeys.Contains(MakeKey(entry.Context, entry.Key))).ToList()
            : [];

        carried.AddRange(displayed
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Context) &&
                !string.IsNullOrWhiteSpace(entry.Key) &&
                !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => new Translation
            {
                Context = entry.Context,
                Key = entry.Key,
                Value = entry.Value!.Trim(),
            }));

        await translationsManager.UpdateTranslationAsync(target.Value, carried);

        // fresh: true - the immutable document is the CACHED copy, which does not reflect this
        // request's own update until the deferred save commits after the response is written.
        // Building the response from it would show the page its pre-save values (the same
        // one-behind trap the admin menu layout documents).
        return Ok(new TranslationsModel(
            cultures,
            target.Value,
            target.CanEdit,
            await BuildGroupsAsync(target.Value, fresh: true)));
    }

    /// <summary>
    /// Every translatable string for the culture: the live descriptors from all providers,
    /// overlaid with stored values - plus every stored entry no descriptor covers, flagged as
    /// an orphan and slotted into its context's group.
    /// </summary>
    private async Task<TranslationGroup[]> BuildGroupsAsync(string culture, bool fresh = false)
    {
        var document = fresh
            ? await translationsManager.LoadTranslationsDocumentAsync()
            : await translationsManager.GetTranslationsDocumentAsync();

        // The PO layer under the store: rows without a stored value surface what would
        // actually render (the shipped catalog translation) as Fallback, so the editor
        // shows the same thing the menu does. Menu-caption rows get the recorded source
        // contexts (the page registry entries' declaring types, unioned per caption - rows have no item identity), then the
        // *.AdminMenu tier, mirroring the caption resolver.
        var poIndexes = PoTranslationLookup.BuildCultureChainIndexes(
            localizationManager, CultureInfo.GetCultureInfo(culture));
        var sourcesByCaption = (await pageRegistry.GetDeclaredAsync()).InShell(Crest.Routing.ShellId.Admin)
            .Where(entry => entry.Menu is { CaptionIsData: false } && entry.DeclaredBy is not null)
            .GroupBy(entry => entry.Menu!.Caption, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.DeclaredBy!).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        // First-wins on duplicate (context, key) pairs rather than ToDictionary's throw - a
        // store that somehow holds duplicates must degrade to showing one row, not 500 the
        // whole editor (the stock controller's exact failure mode; the merge-save then
        // de-duplicates the store on the next save since the pair lands in the displayed set).
        var stored = new Dictionary<string, Translation>(StringComparer.OrdinalIgnoreCase);
        if (document.Translations.TryGetValue(culture, out var entries))
        {
            foreach (var entry in entries)
            {
                stored.TryAdd(MakeKey(entry.Context, entry.Key), entry);
            }
        }

        var rows = new List<TranslationString>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in localizationDataProviders)
        {
            foreach (var descriptor in await provider.GetDescriptorsAsync())
            {
                var key = MakeKey(descriptor.Context, descriptor.Name);
                if (!covered.Add(key))
                {
                    continue;
                }

                stored.TryGetValue(key, out var entry);
                var value = entry?.Value ?? string.Empty;
                rows.Add(new TranslationString(
                    descriptor.Context,
                    descriptor.Name,
                    value,
                    Orphan: false,
                    Fallback: value.Length > 0 ? null : ResolvePoFallback(poIndexes, sourcesByCaption, descriptor.Context, descriptor.Name)));
            }
        }

        foreach (var (key, entry) in stored)
        {
            if (!covered.Contains(key))
            {
                rows.Add(new TranslationString(entry.Context, entry.Key, entry.Value, Orphan: true));
            }
        }

        return rows
            .GroupBy(row => row.Context, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TranslationGroup(
                group.Key,
                group.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private async Task<TranslationCulture[]> GetAllowedCulturesAsync()
    {
        var supported = await localizationService.GetSupportedCulturesAsync();
        var canManageAll = await authorization.AuthorizeAsync(User, DataLocalizationPermissions.ManageTranslations);
        var canView = await authorization.AuthorizeAsync(User, DataLocalizationPermissions.ViewDynamicTranslations);

        var cultures = new List<TranslationCulture>();
        foreach (var name in supported)
        {
            var cultureInfo = CultureInfo.GetCultureInfo(name);
            var label = string.IsNullOrEmpty(cultureInfo.DisplayName) ? cultureInfo.NativeName : cultureInfo.DisplayName;

            var canEdit = canManageAll ||
                await authorization.AuthorizeAsync(User, DataLocalizationPermissions.CreateCulturePermission(name, label));

            if (canEdit || canView)
            {
                cultures.Add(new TranslationCulture(name, label, canEdit));
            }
        }

        return [.. cultures];
    }

    private static string? ResolvePoFallback(
        IReadOnlyList<Dictionary<string, List<(string Context, string Value)>>> poIndexes,
        IReadOnlyDictionary<string, List<string>> sourcesByCaption,
        string context,
        string key)
    {
        var isMenuCaption = context.StartsWith(
            Crest.AdminMenu.DataLocalizationContext.AdminMenu(null), StringComparison.OrdinalIgnoreCase);

        return PoTranslationLookup.Resolve(
            poIndexes,
            key,
            isMenuCaption ? PoTranslationLookup.AdminMenuContextSuffix : null,
            isMenuCaption && sourcesByCaption.TryGetValue(key, out var sourceContexts) ? sourceContexts : null);
    }

    private static string MakeKey(string context, string key) => $"{context}|{key}";
}
