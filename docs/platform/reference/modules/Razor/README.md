# Razor Helpers

Many extensions methods are available in Razor with `@Crest`.

## Razor extensions

| Method                                                                                                                                        | Module                                                                            | Description                                                                                                             |
|-----------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `DisplayAsync(ContentItem content, string displayType = "")`                                                                                  | Crest.ContentManagement.Display                                             | Renders a content item with the corresponding display type.                                                             |
| `GetContentCultureAsync(ContentItem contentItem)`                                                                                             | Crest.ContentLocalization                                                   | Returns the culture for a given ContentItem.                                                                            |
| `CultureDir()`                                                                                                                                | Crest.DisplayManagement                                                     | Returns the current culture direction.                                                                                  |
| `CultureName()`                                                                                                                               | Crest.DisplayManagement                                                     | Returns the current culture name.                                                                                       |
| `ResourceUrl(string resourcePath, bool? appendVersion = null)`                                                                                | Crest.ResourceManagement                                                    | Prefixes the Cdn Base URL to the specified resource path.                                                               |
| `GetContentItemIdByAliasAsync(string alias)`                                                                                                  | Crest.Alias                                                                 | Returns a content item id from its alias. Ex: `carousel`                                                                |
| `GetContentItemIdBySlugAsync(string slug)`                                                                                                    | Crest.Autoroute                                                             | Returns a content item id from its slug. Ex: `myblog/my-blog-post`                                                      |
| `GetContentItemIdByHandleAsync(string handle)`                                                                                                | Crest.Contents                                                              | Returns a content item id from its handle. Ex: `alias:carousel`, `slug:myblog/my-blog-post`                             |
| `GetContentItemByAliasAsync(string alias, bool latest = false)`                                                                               | Crest.Alias                                                                 | Loads a content item by its alias, seeking the latest version or not. Ex: `carousel`                                    |
| `GetContentItemBySlugAsync(string slug, bool latest = false)`                                                                                 | Crest.Autoroute                                                             | Loads a content item by its slug, seeking the latest version or not. Ex: `slug:myblog/my-blog-post`                     |
| `GetContentItemByHandleAsync(string handle, bool latest = false)`                                                                             | Crest.Contents                                                              | Loads a content item by its handle, seeking the latest version or not. Ex: `alias:carousel`, `slug:myblog/my-blog-post` |
| `GetContentItemByIdAsync(string contentItemId, bool latest = false)`                                                                          | Crest.Contents                                                              | Loads a content item by its id.                                                                                         |
| `GetContentItemsByIdAsync(IEnumerable<string> contentItemIds, bool latest = false)`                                                           | Crest.Contents                                                              | Loads a list of content items by their ids.                                                                             |
| `GetContentItemByVersionIdAsync(string contentItemVersionId)`                                                                                 | Crest.Contents                                                              | Loads a content item by its version id.                                                                                 |
| `QueryContentItemsAsync(Func<IQuery<ContentItem, ContentItemIndex>, IQuery<ContentItem>> query)`                                              | Crest.Contents                                                              | Query content items.                                                                                                    |
| `GetRecentContentItemsByContentTypeAsync(string contentType, int maxContentItems = 10)`                                                       | Crest.Contents                                                              | Loads content items of a specific type.                                                                                 |
| `LiquidToHtmlAsync(string liquid)`                                                                                                            | [Crest.Liquid](../../modules/Liquid/README.md#razor-helpers)                | Parses a liquid string to HTML.                                                                                         |
| `LiquidToHtmlAsync(string liquid, object model)`                                                                                              | [Crest.Liquid](../../modules/Liquid/README.md#razor-helpers)                | Parses a liquid string to HTML.                                                                                         |
| `SanitizeHtml(string html)`                                                                                                                   | [Crest.Infrastructure](../Sanitizer/README.md#razor-helper)                 | Sanitizes an HTML string.                                                                                               |
| `QueryListItemsCountAsync(string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)`                           | Crest.Lists                                                                 | Returns list count.                                                                                                     |
| `QueryListItemsAsync(string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)`                                | [Crest.List](../../modules/Lists/README.md#platform-helpers)                 | Returns list items.                                                                                                     |
| `MarkdownToHtmlAsync(string markdown, bool sanitize = true, bool renderLiquid = false)`                                                       | [Crest.Markdown](../../modules/Markdown/README.md#razor-helper)             | Converts Markdown string to HTML.                                                                                       |
| `AssetUrl(string assetPath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, bool appendVersion = false)` | [Crest.Media](../../modules/Media/README.md#razor-helpers)                  | Returns the relative URL of the specifier asset path with optional resizing parameters.                                 |
| `ImageResizeUrl(string imagePath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined)`                       | [Crest.Media](../../modules/Media/README.md#razor-helpers)                  | Returns a URL with custom resizing parameters for an existing image path.                                               |
| `ContentQueryAsync(string queryName)`                                                                                                         | [Crest.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of Content items                                                                                         |
| `ContentQueryAsync(string queryName, IDictionary<string, object> parameters)`                                                                 | [Crest.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of Content items                                                                                         |
| `QueryAsync(string liquid, object model)`                                                                                                     | [Crest.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of objects                                                                                               |
| `QueryAsync(string queryName, IDictionary<string, object> parameters)`                                                                        | [Crest.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of objects                                                                                               |
| `ShortcodesToHtmlAsync(string html, object model = null)`                                                                                     | [Crest.Shortcodes](../../modules/Shortcodes/README.md#rendering-shortcodes) | Renders shortcodes.                                                                                                     |
| `GetTaxonomyTermAsync(string taxonomyContentItemId, string termContentItemId)`                                                                | [Crest.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Returns a the term from its content item id and taxonomy.                                                               |
| `GetInheritedTermsAsync(string taxonomyContentItemId, string termContentItemId)`                                                              | [Crest.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Returns the list of terms including their parents.                                                                      |
| `QueryCategorizedContentItemsAsync(string taxonomy(Func<IQuery<ContentItem, TaxonomyIndex>, IQuery<ContentItem>> query)`                      | [Crest.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Query content items.                                                                                                    |

## How to use

If you want to use an extension method in a view, you can inject an `IPlatformHelper` named `Crest` at the top of your file:

```csharp
@inject Crest.IPlatformHelper Platform
```

In `Crest.DisplayManagement.Razor`, there is a RazorPage that already has a public property `Crest` that you can use to call an extension method or the current `HttpContext`.

If you want to use an Crest helper in a controller, you can inject an instance in the constructor:

```csharp
private IPlatformHelper _orchard;

public MyClass(IPlatformHelper orchard)
{
 _orchard = orchard;
}
```

!!! note
    If the extension method you want to use cannot be found (in a Theme for example), do not forget to reference the corresponding module.
