using Grove.Settings;

namespace Grove.Media.Services;

/// <summary>A branding rendition's media path as the URL the media store serves it at.</summary>
public sealed class MediaBrandingUrlResolver(IMediaFileStore mediaFileStore) : IBrandingUrlResolver
{
    public string ResolveUrl(string mediaPath) =>
        string.IsNullOrWhiteSpace(mediaPath) ? null : mediaFileStore.MapPathToPublicUrl(mediaPath.TrimStart('/'));
}
