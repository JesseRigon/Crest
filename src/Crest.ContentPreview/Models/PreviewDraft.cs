using Crest.ContentManagement;

namespace Crest.ContentPreview.Models;

internal sealed class PreviewDraft
{
    public ContentItem ContentItem { get; set; }
    public string PreviewUrl { get; set; }
}
