using Grove.Media.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Grove.Media.TagHelpers;

[HtmlTargetElement(ImageTagName, Attributes = AssetSrcAttributeName)]
public class ImageTagHelper : TagHelper
{
    private const string AssetSrcAttributeName = "asset-src";

    private const string ImageTagName = "img";

    private const string SourceAttributeName = "src";

    private const string AppendVersionAttributeName = "asp-append-version";

    private readonly IMediaReferenceResolver _references;

    private readonly IFileVersionProvider _fileVersionProvider;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public override int Order => -10;

    [HtmlAttributeName(AssetSrcAttributeName)]
    public string AssetSrc { get; set; }

    /// <summary>
    /// Value indicating if file version should be appended to the src url.
    /// </summary>
    /// <remarks>
    /// If <c>true</c> then a query string "v" with the encoded content of the file is added.
    /// </remarks>
    [HtmlAttributeName(AppendVersionAttributeName)]
    public bool AppendVersion { get; set; }

    public ImageTagHelper(
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
        if (string.IsNullOrEmpty(AssetSrc))
        {
            return;
        }

        // A placement id, a /files or /media URL, or a media library path (ruling F1).
        var resolvedUrl = await _references.GetUrlAsync(AssetSrc);

        if (AppendVersion && _fileVersionProvider != null)
        {
            output.Attributes.SetAttribute(SourceAttributeName, _fileVersionProvider.AddFileVersionToPath(_httpContextAccessor.HttpContext.Request.PathBase, resolvedUrl));
        }
        else
        {
            output.Attributes.SetAttribute(SourceAttributeName, resolvedUrl);
        }
    }
}
