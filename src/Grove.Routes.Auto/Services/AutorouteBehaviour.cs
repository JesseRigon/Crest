using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.Routes.Auto.Indexes;
using Grove.Routes.Auto.Models;
using Grove.Routes.Auto.ViewModels;
using Grove.Contents.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Routing;
using Grove.Environment.Cache;
using Grove.Data.Documents;
using Grove.Display.Liquid;
using Grove.Data.Localization;
using Grove.Infrastructure.Settings;
using YesSql;
using YesSql.Services;

namespace Grove.Routes.Auto.Services;

/// <summary>
/// What the permalink's system flows do (<see cref="Workflows.AutorouteWorkflowProvider"/>): an empty
/// path is generated from the attachment's pattern and contained items' paths kept unique; a path is
/// valid and unique, and the home page takes its own right; a clone gets a path of its own; publishing,
/// unpublishing and removing keep the route entries, the home route and the route cache; a
/// translation regenerates its path.
/// </summary>
public class AutorouteBehaviour
{
    private readonly IAutorouteEntries _entries;
    private readonly AutorouteOptions _options;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISiteService _siteService;
    private readonly ITagCache _tagCache;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    protected readonly IStringLocalizer S;

    private IContentManager _contentManager;

    public AutorouteBehaviour(
        IAutorouteEntries entries,
        IOptions<AutorouteOptions> options,
        ILiquidTemplateManager liquidTemplateManager,
        IContentDefinitionManager contentDefinitionManager,
        ISiteService siteService,
        ITagCache tagCache,
        ISession session,
        IServiceProvider serviceProvider,
        IStringLocalizer<AutorouteBehaviour> stringLocalizer)
    {
        _entries = entries;
        _options = options.Value;
        _liquidTemplateManager = liquidTemplateManager;
        _contentDefinitionManager = contentDefinitionManager;
        _siteService = siteService;
        _tagCache = tagCache;
        _session = session;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>Keeps the route entries, the home route and the route cache on publishing, unpublishing and removing.</summary>
    public async Task RegisterEntriesAsync(ContentItem contentItem, string point)
    {
        if (!contentItem.TryGet<AutorouteFieldSet>(out var part))
        {
            return;
        }

        if (point != ContentLifecyclePoints.Publishing)
        {
            if (!string.IsNullOrWhiteSpace(part.Path))
            {
                // Update entries from the index table after the session is committed.
                await _entries.UpdateEntriesAsync();

                // Evict any dependent item from cache.
                await RemoveTagAsync(part);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(part.Path))
        {
            if (part.RouteContainedItems)
            {
                _contentManager ??= _serviceProvider.GetRequiredService<IContentManager>();
                var containedAspect = await _contentManager.PopulateAspectAsync<EmbeddedContentItemsAspect>(contentItem);
                await CheckContainedHomeRouteAsync(contentItem.ContentItemId, containedAspect, (JsonObject)contentItem.Content);
            }

            // Update entries from the index table after the session is committed.
            await _entries.UpdateEntriesAsync();
        }

        if (!string.IsNullOrWhiteSpace(part.Path) && !part.Disabled && part.SetHomepage)
        {
            await SetHomeRouteAsync(part, homeRoute =>
            {
                homeRoute[_options.ContentItemIdKey] = contentItem.ContentItemId;
                homeRoute.Remove(_options.JsonPathKey);
            });
        }

        // Evict any dependent item from cache.
        await RemoveTagAsync(part);
    }

    /// <summary>The refusal of a path that is invalid or used, or of a home page the user may not set; null when it is fine.</summary>
    public async Task<ContentFieldRefusal> RefusalAsync(ContentItem contentItem)
    {
        if (!contentItem.TryGet<AutorouteFieldSet>(out var part))
        {
            return null;
        }

        var partName = ContentFieldSetName.Of<AutorouteFieldSet>();

        // Setting the home page takes its own right, whoever saves the item.
        if (part.SetHomepage)
        {
            var http = _serviceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()?.HttpContext;
            var authorization = _serviceProvider.GetService<IAuthorizationService>();
            if (http?.User is { } user && authorization is not null && !await authorization.AuthorizeAsync(user, AutoroutePermissions.SetHomepage))
            {
                return new ContentFieldRefusal(S["You may not set the home page."], $"{partName}.{AutorouteFieldSet.SetHomepageField}");
            }
        }

        // Only validate the path if it's not empty.
        if (string.IsNullOrWhiteSpace(part.Path))
        {
            return null;
        }

        foreach (var item in part.ValidatePathFieldValue(S))
        {
            return new ContentFieldRefusal(item.ErrorMessage, $"{partName}.{AutorouteFieldSet.PathField}");
        }

        return await IsAbsolutePathUniqueAsync(part.Path, contentItem.ContentItemId)
            ? null
            : new ContentFieldRefusal(S["Your permalink is already in use."], $"{partName}.{AutorouteFieldSet.PathField}");
    }

    /// <summary>Generates an empty path from the attachment's pattern, and keeps the contained items' paths unique.</summary>
    public async Task GenerateAsync(ContentItem contentItem)
    {
        // The flow runs only where the field set is attached: an item that does not hold it yet gets it.
        var part = contentItem.GetOrCreate<AutorouteFieldSet>();

        await GenerateContainerPathFromPatternAsync(part);
        await GenerateContainedPathsFromPatternAsync(contentItem, part);
    }

    /// <summary>A clone's path: its source's, made unique; never the home page.</summary>
    public async Task CloneAsync(ContentItem clone)
    {
        if (!clone.TryGet<AutorouteFieldSet>(out var part))
        {
            return;
        }

        if (!string.IsNullOrEmpty(part.Path))
        {
            part.Path = await GenerateUniqueAbsolutePathAsync(part.Path, clone.ContentItemId);
        }

        part.SetHomepage = false;
        part.Apply();

        await GenerateContainedPathsFromPatternAsync(clone, part);
    }

    /// <summary>A translation's path is cleared, to be generated again from the pattern in its culture.</summary>
    public Task ClearForTranslationAsync(ContentItem translation)
    {
        if (translation.Has<AutorouteFieldSet>())
        {
            translation.Alter<AutorouteFieldSet>(part => part.Path = null);
        }

        return Task.CompletedTask;
    }

    private async Task SetHomeRouteAsync(AutorouteFieldSet part, Action<RouteValueDictionary> action)
    {
        var site = await _siteService.LoadSiteSettingsAsync();

        site.HomeRoute ??= [];

        var homeRoute = site.HomeRoute;

        foreach (var entry in _options.GlobalRouteValues)
        {
            homeRoute[entry.Key] = entry.Value;
        }

        action.Invoke(homeRoute);

        // Once we took the flag into account we can dismiss it.
        part.SetHomepage = false;
        part.Apply();

        await _siteService.UpdateSiteSettingsAsync(site);
    }

    // The route cache entry is evicted once the unit commits: a cancelled unit (a refused save, a
    // preview) leaves it alone.
    private Task RemoveTagAsync(AutorouteFieldSet part)
    {
        var tag = $"slug:{part.Path}";
        _serviceProvider.GetRequiredService<IDocumentStore>().AfterCommit(() => _tagCache.RemoveTagAsync(tag));
        return Task.CompletedTask;
    }

    private async Task CheckContainedHomeRouteAsync(string containerContentItemId, EmbeddedContentItemsAspect containedAspect, JsonObject content)
    {
        foreach (var accessor in containedAspect.ReferenceableAccessors)
        {
            var jItems = accessor.Invoke(content);

            foreach (var jItem in jItems.Cast<JsonObject>())
            {
                var contentItem = jItem.ToObject<ContentItem>();
                var handlerAspect = await _contentManager.PopulateAspectAsync<RouteHandlerAspect>(contentItem);

                if (!handlerAspect.Disabled)
                {
                    // Only an autoroute part, not a default handler aspect can set itself as the homepage.
                    if (contentItem.TryGet<AutorouteFieldSet>(out var autorouteFieldSet) && autorouteFieldSet.SetHomepage)
                    {
                        await SetHomeRouteAsync(autorouteFieldSet, homeRoute =>
                        {
                            homeRoute[_options.ContentItemIdKey] = containerContentItemId;
                            homeRoute[_options.JsonPathKey] = jItem.GetNormalizedPath();
                        });

                        break;
                    }
                }
            }
        }
    }

    private async Task GenerateContainedPathsFromPatternAsync(ContentItem contentItem, AutorouteFieldSet part)
    {
        // Validate contained content item routes if container has valid path.
        if (string.IsNullOrWhiteSpace(part.Path) || !part.RouteContainedItems)
        {
            return;
        }

        _contentManager ??= _serviceProvider.GetRequiredService<IContentManager>();
        var containedAspect = await _contentManager.PopulateAspectAsync<EmbeddedContentItemsAspect>(contentItem);

        // Build the entries for this content item to evaluate for duplicates.
        var entries = new List<AutorouteEntry>();
        await PopulateContainedContentItemRoutesAsync(entries, part.ContentItem.ContentItemId, containedAspect, (JsonObject)contentItem.Content, part.Path);

        await ValidateContainedContentItemRoutesAsync(entries, part.ContentItem.ContentItemId, containedAspect, (JsonObject)contentItem.Content, part.Path);
    }

    private async Task PopulateContainedContentItemRoutesAsync(List<AutorouteEntry> entries, string containerContentItemId, EmbeddedContentItemsAspect containedContentItemsAspect, JsonObject content, string basePath)
    {
        foreach (var accessor in containedContentItemsAspect.ReferenceableAccessors)
        {
            var jItems = accessor.Invoke(content);

            foreach (var jItem in jItems.Cast<JsonObject>())
            {
                var contentItem = jItem.ToObject<ContentItem>();
                var handlerAspect = await _contentManager.PopulateAspectAsync<RouteHandlerAspect>(contentItem);

                if (!handlerAspect.Disabled)
                {
                    var path = handlerAspect.Path;
                    if (!handlerAspect.Absolute)
                    {
                        path = (basePath.EndsWith('/') ? basePath : basePath + '/') + handlerAspect.Path.TrimStart('/');
                    }

                    entries.Add(new AutorouteEntry(containerContentItemId, path, contentItem.ContentItemId, jItem.GetNormalizedPath())
                    {
                        DocumentId = contentItem.Id,
                    });
                }

                var itemBasePath = (basePath.EndsWith('/') ? basePath : basePath + '/') + handlerAspect.Path.TrimStart('/');
                var childrenAspect = await _contentManager.PopulateAspectAsync<EmbeddedContentItemsAspect>(contentItem);
                await PopulateContainedContentItemRoutesAsync(entries, containerContentItemId, childrenAspect, jItem, itemBasePath);
            }
        }
    }

    private async Task ValidateContainedContentItemRoutesAsync(List<AutorouteEntry> entries, string containerContentItemId, EmbeddedContentItemsAspect containedContentItemsAspect, JsonObject content, string basePath)
    {
        foreach (var accessor in containedContentItemsAspect.ReferenceableAccessors)
        {
            var jItems = accessor.Invoke(content);

            foreach (var jItem in jItems.Cast<JsonObject>())
            {
                var contentItem = jItem.ToObject<ContentItem>();
                // This is only relevant if the content items have an autoroute part as we adjust the part value as required to guarantee a unique route.
                // Content items routed only through the handler aspect already guarantee uniqueness.
                if (contentItem.TryGet<AutorouteFieldSet>(out var containedAutorouteFieldSet) && containedAutorouteFieldSet is { Disabled: false, Path.Length: > 0 })
                {
                    var path = containedAutorouteFieldSet.Path;

                    if (containedAutorouteFieldSet.Absolute && !await IsAbsolutePathUniqueAsync(path, contentItem.ContentItemId))
                    {
                        path = await GenerateUniqueAbsolutePathAsync(path, contentItem.ContentItemId);
                        containedAutorouteFieldSet.Path = path;
                        containedAutorouteFieldSet.Apply();

                        // Merge because we have disconnected the content item from it's json owner.
                        jItem.Merge((JsonObject)contentItem.Content, new JsonMergeSettings
                        {
                            MergeArrayHandling = MergeArrayHandling.Replace,
                            MergeNullValueHandling = MergeNullValueHandling.Merge,
                        });
                    }
                    else
                    {
                        var currentItemBasePath = basePath.EndsWith('/') ? basePath : basePath + '/';
                        path = currentItemBasePath + containedAutorouteFieldSet.Path.TrimStart('/');
                        if (!IsRelativePathUnique(entries, path, containedAutorouteFieldSet))
                        {
                            path = GenerateRelativeUniquePath(entries, path, containedAutorouteFieldSet);
                            // Remove base path and update part path.
                            containedAutorouteFieldSet.Path = path[currentItemBasePath.Length..];
                            containedAutorouteFieldSet.Apply();

                            // Merge because we have disconnected the content item from it's json owner.
                            jItem.Merge((JsonObject)contentItem.Content, new JsonMergeSettings
                            {
                                MergeArrayHandling = MergeArrayHandling.Replace,
                                MergeNullValueHandling = MergeNullValueHandling.Merge,
                            });
                        }

                        path = path[currentItemBasePath.Length..];
                    }

                    var containedItemBasePath = (basePath.EndsWith('/') ? basePath : basePath + '/') + path;
                    var childItemAspect = await _contentManager.PopulateAspectAsync<EmbeddedContentItemsAspect>(contentItem);
                    await ValidateContainedContentItemRoutesAsync(entries, containerContentItemId, childItemAspect, jItem, containedItemBasePath);
                }
            }
        }
    }

    internal static string GenerateRelativeUniquePath(List<AutorouteEntry> entries, string path, AutorouteFieldSet context)
    {
        var version = 1;
        var unversionedPath = path;

        var versionSeparatorPosition = path.LastIndexOf('-');
        if (versionSeparatorPosition > -1 && int.TryParse(path[versionSeparatorPosition..].TrimStart('-'), out var parsedVersion))
        {
            version = parsedVersion;
            unversionedPath = path[..versionSeparatorPosition];
        }

        while (true)
        {
            // Find the max version already used for this unversionedPath in entries
            var maxVersion = entries
                .Where(e => e.Path.StartsWith(unversionedPath + "-", StringComparison.OrdinalIgnoreCase))
                .Select(e => int.TryParse(e.Path[(unversionedPath.Length + 1)..], out var v) ? v : 0)
                .DefaultIfEmpty(0)
                .Max();

            var nextVersion = Math.Max(version, maxVersion) + 1;

            while (true)
            {
                var versionedPath = $"{unversionedPath}-{nextVersion}";
                if (IsRelativePathUnique(entries, versionedPath, context))
                {
                    var entry = entries.SingleOrDefault(e => e.ContainedContentItemId == context.ContentItem.ContentItemId);
                    if (entry == null)
                    {
                        // Add new entry for this contained item if it doesn't exist yet, preventing a null reference exception
                        entry = new AutorouteEntry(context.ContentItem.ContentItemId, versionedPath, context.ContentItem.ContentItemId);
                        entries.Add(entry);
                    }
                    entry.Path = versionedPath;

                    return versionedPath;
                }
                nextVersion++;
            }
        }
    }


    private static bool IsRelativePathUnique(List<AutorouteEntry> entries, string path, AutorouteFieldSet context)
    {
        var result = !entries.Any(e => context.ContentItem.ContentItemId != e.ContainedContentItemId && string.Equals(e.Path.Trim('/'), path.Trim('/'), StringComparison.OrdinalIgnoreCase));
        return result;
    }

    
    private async Task GenerateContainerPathFromPatternAsync(AutorouteFieldSet part)
    {
        // Compute the Path only if it's empty.
        if (!string.IsNullOrWhiteSpace(part.Path))
        {
            return;
        }

        var pattern = await GetPatternAsync(part);

        if (!string.IsNullOrEmpty(pattern))
        {
            var model = new AutorouteFieldSetViewModel()
            {
                Path = part.Path,
                AutorouteFieldSet = part,
                ContentItem = part.ContentItem,
            };

            _contentManager ??= _serviceProvider.GetRequiredService<IContentManager>();

            var cultureAspect = await _contentManager.PopulateAspectAsync(part.ContentItem, new CultureAspect());

            var cultureOptions = _serviceProvider.GetService<IOptions<CultureOptions>>().Value;

            using (CultureScope.Create(cultureAspect.Culture, ignoreSystemSettings: cultureOptions.IgnoreSystemSettings))
            {
                part.Path = await _liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, model,
                    new Dictionary<string, FluidValue>() { [nameof(ContentItem)] = new ObjectValue(model.ContentItem) });
            }

            part.Path = part.Path.ReplaceLineEndings(string.Empty);

            if (part.Path?.Length > AutorouteFieldSet.MaxPathLength)
            {
                part.Path = part.Path[..AutorouteFieldSet.MaxPathLength];
            }

            if (!await IsAbsolutePathUniqueAsync(part.Path, part.ContentItem.ContentItemId))
            {
                part.Path = await GenerateUniqueAbsolutePathAsync(part.Path, part.ContentItem.ContentItemId);
            }

            part.Apply();
        }
    }

    /// <summary>
    /// Get the pattern from the AutorouteFieldSetSettings property for its type.
    /// </summary>
    private async Task<string> GetPatternAsync(AutorouteFieldSet part)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
        var contentTypeFieldSetDefinition = contentTypeDefinition.FieldSets.FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, ContentFieldSetName.Of<AutorouteFieldSet>(), StringComparison.Ordinal));
        var pattern = contentTypeFieldSetDefinition.GetSettings<AutorouteFieldSetSettings>().Pattern;

        return pattern;
    }

    private async Task<string> GenerateUniqueAbsolutePathAsync(string path, string contentItemId)
    {
        var version = 1;
        var unversionedPath = path;

        var versionSeparatorPosition = path.LastIndexOf('-');
        if (versionSeparatorPosition > -1 && int.TryParse(path[versionSeparatorPosition..].TrimStart('-'), out version))
        {
            unversionedPath = path[..versionSeparatorPosition];
        }

        while (true)
        {
            // Unversioned length + separator char + version length.
            var quantityCharactersToTrim = unversionedPath.Length + 1 + version.ToString().Length - AutorouteFieldSet.MaxPathLength;
            if (quantityCharactersToTrim > 0)
            {
                unversionedPath = unversionedPath[..^quantityCharactersToTrim];
            }

            var versionedPath = $"{unversionedPath}-{version++}";
            if (await IsAbsolutePathUniqueAsync(versionedPath, contentItemId))
            {
                return versionedPath;
            }
        }
    }

    private async Task<bool> IsAbsolutePathUniqueAsync(string path, string contentItemId)
    {
        path = path.Trim('/');
        var paths = new string[] { path, "/" + path, path + "/", "/" + path + "/" };

        var possibleConflicts = await _session.QueryIndex<AutorouteIndex>(o => (o.Published || o.Latest) && o.Path.IsIn(paths)).ListAsync();
        if (possibleConflicts.Any(x => x.ContentItemId != contentItemId && x.ContainedContentItemId != contentItemId))
        {
            return false;
        }

        return true;
    }
}
