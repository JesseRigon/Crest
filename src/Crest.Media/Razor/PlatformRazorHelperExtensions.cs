using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest;
using Crest.Media;
using Crest.Media.Processing;
using Crest.Media.Fields;
using Crest.Media.Processing;
using Crest.Media.Services;

#pragma warning disable CA1050 // Declare types in namespaces
public static class MediaPlatformRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns the relative URL of the specified asset path with optional resizing parameters.
    /// </summary>
    public static string AssetUrl(this IPlatformHelper platformHelper, string assetPath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, bool appendVersion = false, int? quality = null, Format format = Format.Undefined, Anchor anchor = null, string bgColor = null, bool? autoorient = null)
    {
        var mediaFileStore = platformHelper.HttpContext.RequestServices.GetService<IMediaFileStore>();

        if (mediaFileStore == null)
        {
            return assetPath;
        }

        assetPath = assetPath.RemoveQueryString(out string queryString);
        var resolvedAssetPath = mediaFileStore.MapPathToPublicUrl(assetPath) + queryString;

        if (appendVersion)
        {
            var fileVersionProvider = platformHelper.HttpContext.RequestServices.GetService<IFileVersionProvider>();

            resolvedAssetPath = fileVersionProvider.AddFileVersionToPath(platformHelper.HttpContext.Request.PathBase, resolvedAssetPath);
        }

        return platformHelper.ImageResizeUrl(resolvedAssetPath, width, height, resizeMode, quality, format, anchor, bgColor, autoorient);
    }

    /// <summary>
    /// Returns the relative URL of the specified asset path for a media profile with optional resizing parameters.
    /// </summary>
    public static Task<string> AssetProfileUrlAsync(this IPlatformHelper platformHelper, string assetPath, string imageProfile, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, bool appendVersion = false, int? quality = null, Format format = Format.Undefined, Anchor anchor = null, string bgcolor = null, bool? autoorient = null)
    {
        var mediaFileStore = platformHelper.HttpContext.RequestServices.GetService<IMediaFileStore>();

        if (mediaFileStore == null)
        {
            return Task.FromResult(assetPath);
        }

        assetPath = assetPath.RemoveQueryString(out string queryString);
        var resolvedAssetPath = mediaFileStore.MapPathToPublicUrl(assetPath) + queryString;

        if (appendVersion)
        {
            var fileVersionProvider = platformHelper.HttpContext.RequestServices.GetService<IFileVersionProvider>();

            resolvedAssetPath = fileVersionProvider.AddFileVersionToPath(platformHelper.HttpContext.Request.PathBase, resolvedAssetPath);
        }

        return platformHelper.ImageProfileResizeUrlAsync(resolvedAssetPath, imageProfile, width, height, resizeMode, quality, format, anchor, bgcolor, autoorient);
    }

    /// <summary>
    /// Returns a URL with custom resizing parameters for an existing image path.
    /// </summary>
    public static string ImageResizeUrl(this IPlatformHelper platformHelper, string imagePath, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, int? quality = null, Format format = Format.Undefined, Anchor anchor = null, string bgcolor = null, bool? autoorient = null)
    {
        var resizedUrl = MediaImageUrlFormatter.GetImageResizeUrl(imagePath, null, width, height, resizeMode, quality, format, anchor, bgcolor, autoorient);

        return platformHelper.TokenizeUrl(resizedUrl);
    }

    /// <summary>
    /// Returns a URL with custom resizing parameters for a media profile for an existing image path.
    /// </summary>
    public static async Task<string> ImageProfileResizeUrlAsync(this IPlatformHelper platformHelper, string imagePath, string imageProfile, int? width = null, int? height = null, ResizeMode resizeMode = ResizeMode.Undefined, int? quality = null, Format format = Format.Undefined, Anchor anchor = null, string bgcolor = null, bool? autoorient = null)
    {
        var mediaProfileService = platformHelper.HttpContext.RequestServices.GetRequiredService<IMediaProfileService>();
        var queryStringParams = await mediaProfileService.GetMediaProfileCommands(imageProfile);

        var resizedUrl = MediaImageUrlFormatter.GetImageResizeUrl(imagePath, queryStringParams, width, height, resizeMode, quality, format, anchor, bgcolor, autoorient);

        return platformHelper.TokenizeUrl(resizedUrl);
    }

    private static string TokenizeUrl(this IPlatformHelper platformHelper, string url)
    {
        var mediaOptions = platformHelper.HttpContext.RequestServices.GetService<IOptions<MediaOptions>>().Value;
        if (mediaOptions.UseTokenizedQueryString)
        {
            var mediaTokenService = platformHelper.HttpContext.RequestServices.GetService<IMediaTokenService>();

            url = mediaTokenService.AddTokenToPath(url);
        }

        return url;
    }
}
