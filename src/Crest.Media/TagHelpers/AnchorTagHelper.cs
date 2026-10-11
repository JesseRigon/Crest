using Crest.Media.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Crest.Media.TagHelpers;

[HtmlTargetElement(AnchorTagName, Attributes = AssetHrefAttributeName)]
public class AnchorTagHelper : TagHelper
{
    private const string AssetHrefAttributeName = "asset-href";
    private const string AnchorTagName = "a";
    private const string HrefAttributeName = "href";
    private const string AppendVersionAttributeName = "asp-append-version";

    private readonly IMediaReferenceResolver _references;
    private readonly IFileVersionProvider _fileVersionProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public override int Order => -10;

    [HtmlAttributeName(AssetHrefAttributeName)]
    public string AssetHref { get; set; }

    /// <summary>
    /// Value indicating if file version should be appended to href url.
    /// </summary>
    /// <remarks>
    /// If <c>true</c> then a query string "v" with the encoded content of the file is added.
    /// </remarks>
    [HtmlAttributeName(AppendVersionAttributeName)]
    public bool AppendVersion { get; set; }

    public AnchorTagHelper(
        IMediaReferenceResolver references,
        IHttpContextAccessor httpContextAccessor,
        IFileVersionProvider fileVersionProvider
        )
    {
        _references = references;
        _httpContextAccessor = httpContextAccessor;
        _fileVersionProvider = fileVersionProvider;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrEmpty(AssetHref))
        {
            return;
        }

        // A placement id, a /files or /media URL, or a media library path (ruling F1).
        var resolvedUrl = await _references.GetUrlAsync(AssetHref);

        if (AppendVersion && _fileVersionProvider != null)
        {
            output.Attributes.SetAttribute(HrefAttributeName, _fileVersionProvider.AddFileVersionToPath(_httpContextAccessor.HttpContext.Request.PathBase, resolvedUrl));
        }
        else
        {
            output.Attributes.SetAttribute(HrefAttributeName, resolvedUrl);
        }
    }
}
