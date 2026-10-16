using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Grove.Contents.Alias.Models;
using Grove.Contents.Alias.Settings;
using Grove.Contents.Alias.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Environment.Cache;
using Grove.Display.Liquid;
using YesSql;
using Grove.Data.Documents;

namespace Grove.Contents.Alias.Services;

/// <summary>
/// What the alias's system flows do (<see cref="Workflows.AliasWorkflowProvider"/>): an empty alias
/// is generated from the attachment's pattern, an alias is unique and fits the index, a clone gets an
/// alias of its own, and the alias cache is invalidated when the item is published, unpublished or
/// removed.
/// </summary>
public sealed class AliasBehaviour(
    IContentDefinitionManager contentDefinitionManager,
    ITagCache tagCache,
    IDocumentStore documentStore,
    ILiquidTemplateManager liquidTemplateManager,
    ISession session,
    IStringLocalizer<AliasBehaviour> S)
{
    /// <summary>The refusal of an alias too long or already used; null when it is fine.</summary>
    public async Task<string> ErrorAsync(ContentItem item)
    {
        if (!item.TryGet<AliasFieldSet>(out var part) || string.IsNullOrWhiteSpace(part.Alias))
        {
            return null;
        }

        await foreach (var error in part.ValidateAsync(S, session))
        {
            return error.ErrorMessage;
        }

        return null;
    }

    /// <summary>Generates an empty alias from the attachment's pattern, unique.</summary>
    public async Task GenerateAsync(ContentItem item)
    {
        // The flow runs only where the field set is attached: an item that does not hold it yet gets it.
        var part = item.GetOrCreate<AliasFieldSet>();
        if (!string.IsNullOrEmpty(part.Alias))
        {
            return;
        }

        var pattern = await PatternAsync(item);
        if (string.IsNullOrEmpty(pattern))
        {
            return;
        }

        var model = new AliasFieldSetViewModel
        {
            Alias = part.Alias,
            AliasFieldSet = part,
            ContentItem = item,
        };

        var alias = (await liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, model,
            new Dictionary<string, FluidValue> { [nameof(ContentItem)] = new ObjectValue(model.ContentItem) })).ReplaceLineEndings(string.Empty);

        if (alias.Length > AliasFieldSet.MaxAliasLength)
        {
            alias = alias[..AliasFieldSet.MaxAliasLength];
        }

        if (!await part.IsAliasUniqueAsync(session, alias))
        {
            alias = await UniqueAsync(alias, part);
        }

        item.Alter<AliasFieldSet>(aliasFieldSet => aliasFieldSet.Alias = alias);
    }

    /// <summary>A clone's alias: its source's, made unique.</summary>
    public async Task MakeUniqueAsync(ContentItem clone)
    {
        if (!clone.TryGet<AliasFieldSet>(out var part) || string.IsNullOrEmpty(part.Alias))
        {
            return;
        }

        var alias = await UniqueAsync(part.Alias, part);
        clone.Alter<AliasFieldSet>(aliasFieldSet => aliasFieldSet.Alias = alias);
    }

    /// <summary>The alias cache entry of the item's alias, invalidated once the unit commits (a cancelled unit leaves the cache alone).</summary>
    public Task InvalidateAsync(ContentItem item)
    {
        if (item.TryGet<AliasFieldSet>(out var part) && !string.IsNullOrEmpty(part.Alias))
        {
            var tag = AliasConstants.AliasPrefix + part.Alias;
            documentStore.AfterCommit(() => tagCache.RemoveTagAsync(tag));
        }

        return Task.CompletedTask;
    }

    private async Task<string> PatternAsync(ContentItem item)
    {
        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
        return contentTypeDefinition?.FieldSets
            .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, ContentFieldSetName.Of<AliasFieldSet>(), StringComparison.Ordinal))?
            .GetSettings<AliasFieldSetSettings>().Pattern;
    }

    private async Task<string> UniqueAsync(string alias, AliasFieldSet context)
    {
        var version = 1;
        var unversionedAlias = alias;

        var versionSeparatorPosition = alias.LastIndexOf('-');
        if (versionSeparatorPosition > -1 && int.TryParse(alias[versionSeparatorPosition..].TrimStart('-'), out version))
        {
            unversionedAlias = alias[..versionSeparatorPosition];
        }

        while (true)
        {
            // Unversioned length + separator char + version length.
            var quantityCharactersToTrim = unversionedAlias.Length + 1 + version.ToString().Length - AliasFieldSet.MaxAliasLength;
            if (quantityCharactersToTrim > 0)
            {
                unversionedAlias = unversionedAlias[..^quantityCharactersToTrim];
            }

            var versionedAlias = $"{unversionedAlias}-{version++}";
            if (await context.IsAliasUniqueAsync(session, versionedAlias))
            {
                return versionedAlias;
            }
        }
    }
}
