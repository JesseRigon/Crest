# Razor Helpers

Many extensions methods are available in Razor with `@Grove`.

## Razor extensions

| Method                                                                                                                                        | Module                                                                            | Description                                                                                                             |
|-----------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `DisplayAsync(ContentItem content, string displayType = "")`                                                                                  | Grove.Contents.Management.Display                                             | Renders a content item with the corresponding display type.                                                             |
| `GetContentCultureAsync(ContentItem contentItem)`                                                                                             | Grove.Contents.Localization                                                   | Returns the culture for a given ContentItem.                                                                            |
| `CultureDir()`                                                                                                                                | Grove.Display.Management                                                     | Returns the current culture direction.                                                                                  |
| `CultureName()`                                                                                                                               | Grove.Display.Management                                                     | Returns the current culture name.                                                                                       |
| `ResourceUrl(string resourcePath, bool? appendVersion = null)`                                                                                | Grove.Display.Resources.Management                                                    | Prefixes the Cdn Base URL to the specified resource path.                                                               |
| `GetContentItemIdByAliasAsync(string alias)`                                                                                                  | Grove.Contents.Alias                                                                 | Returns a content item id from its alias. Ex: `carousel`                                                                |
| `GetContentItemIdBySlugAsync(string slug)`                                                                                                    | Grove.Routes.Auto                                                             | Returns a content item id from its slug. Ex: `myblog/my-blog-post`                                                      |
| `GetContentItemIdByHandleAsync(string handle)`                                                                                                | Grove.Contents                                                              | Returns a content item id from its handle. Ex: `alias:carousel`, `slug:myblog/my-blog-post`                             |
| `GetContentItemByAliasAsync(string alias, bool latest = false)`                                                                               | Grove.Contents.Alias                                                                 | Loads a content item by its alias, seeking the latest version or not. Ex: `carousel`                                    |
| `GetContentItemBySlugAsync(string slug, bool latest = false)`                                                                                 | Grove.Routes.Auto                                                             | Loads a content item by its slug, seeking the latest version or not. Ex: `slug:myblog/my-blog-post`                     |
| `GetContentItemByHandleAsync(string handle, bool latest = false)`                                                                             | Grove.Contents                                                              | Loads a content item by its handle, seeking the latest version or not. Ex: `alias:carousel`, `slug:myblog/my-blog-post` |
| `GetContentItemByIdAsync(string contentItemId, bool latest = false)`                                                                          | Grove.Contents                                                              | Loads a content item by its id.                                                                                         |
| `GetContentItemsByIdAsync(IEnumerable<string> contentItemIds, bool latest = false)`                                                           | Grove.Contents                                                              | Loads a list of content items by their ids.                                                                             |
| `GetContentItemByVersionIdAsync(string contentItemVersionId)`                                                                                 | Grove.Contents                                                              | Loads a content item by its version id.                                                                                 |
| `QueryContentItemsAsync(Func<IQuery<ContentItem, ContentItemIndex>, IQuery<ContentItem>> query)`                                              | Grove.Contents                                                              | Query content items.                                                                                                    |
| `GetRecentContentItemsByContentTypeAsync(string contentType, int maxContentItems = 10)`                                                       | Grove.Contents                                                              | Loads content items of a specific type.                                                                                 |
| `LiquidToHtmlAsync(string liquid)`                                                                                                            | [Grove.Display.Liquid](../../modules/Liquid/README.md#razor-helpers)                | Parses a liquid string to HTML.                                                                                         |
| `LiquidToHtmlAsync(string liquid, object model)`                                                                                              | [Grove.Display.Liquid](../../modules/Liquid/README.md#razor-helpers)                | Parses a liquid string to HTML.                                                                                         |
| `SanitizeHtml(string html)`                                                                                                                   | [Grove.Infrastructure](../Sanitizer/README.md#razor-helper)                 | Sanitizes an HTML string.                                                                                               |
| `QueryListItemsCountAsync(string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)`                           | Grove.Display.Lists                                                                 | Returns list count.                                                                                                     |
| `QueryListItemsAsync(string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)`                                | [Grove.List](../../modules/Lists/README.md#platform-helpers)                 | Returns list items.                                                                                                     |
| `MarkdownToHtmlAsync(string markdown, bool sanitize = true, bool renderLiquid = false)`                                                       | [Grove.Contents.Markdown](../../modules/Markdown/README.md#razor-helper)             | Converts Markdown string to HTML.                                                                                       |
| `AssetUrl(string assetPath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, bool appendVersion = false)` | [Grove.Media](../../modules/Media/README.md#razor-helpers)                  | Returns the relative URL of the specifier asset path with optional resizing parameters.                                 |
| `ImageResizeUrl(string imagePath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined)`                       | [Grove.Media](../../modules/Media/README.md#razor-helpers)                  | Returns a URL with custom resizing parameters for an existing image path.                                               |
| `ContentQueryAsync(string queryName)`                                                                                                         | [Grove.Data.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of Content items                                                                                         |
| `ContentQueryAsync(string queryName, IDictionary<string, object> parameters)`                                                                 | [Grove.Data.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of Content items                                                                                         |
| `QueryAsync(string liquid, object model)`                                                                                                     | [Grove.Data.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of objects                                                                                               |
| `QueryAsync(string queryName, IDictionary<string, object> parameters)`                                                                        | [Grove.Data.Queries](../../modules/Queries/README.md#razor-helpers)              | Returns a List of objects                                                                                               |
| `ShortcodesToHtmlAsync(string html, object model = null)`                                                                                     | [Grove.Shortcodes](../../modules/Shortcodes/README.md#rendering-shortcodes) | Renders shortcodes.                                                                                                     |
| `GetTaxonomyTermAsync(string taxonomyContentItemId, string termContentItemId)`                                                                | [Grove.Contents.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Returns a the term from its content item id and taxonomy.                                                               |
| `GetInheritedTermsAsync(string taxonomyContentItemId, string termContentItemId)`                                                              | [Grove.Contents.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Returns the list of terms including their parents.                                                                      |
| `QueryCategorizedContentItemsAsync(string taxonomy(Func<IQuery<ContentItem, TaxonomyIndex>, IQuery<ContentItem>> query)`                      | [Grove.Contents.Taxonomies](../../modules/Taxonomies/README.md#platform-helpers)      | Query content items.                                                                                                    |

## How to use

If you want to use an extension method in a view, you can inject an `IPlatformHelper` named `Grove` at the top of your file:

```csharp
@inject Grove.IPlatformHelper Platform
```

In `Grove.Display.Management.Razor`, there is a RazorPage that already has a public property `Grove` that you can use to call an extension method or the current `HttpContext`.

If you want to use an Grove helper in a controller, you can inject an instance in the constructor:

```csharp
private IPlatformHelper _orchard;

public MyClass(IPlatformHelper orchard)
{
 _orchard = orchard;
}
```

!!! note
    If the extension method you want to use cannot be found (in a Theme for example), do not forget to reference the corresponding module.
