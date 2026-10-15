using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;

namespace Grove.Contents.Localization.Handlers;

public class LocalizationContentContext : ContentContextBase
{
    public ContentItem Original { get; set; }
    public string LocalizationSet { get; set; }
    public string Culture { get; set; }
    public LocalizationContentContext(ContentItem contentItem, ContentItem original, string localizationSet, string culture)
        : base(contentItem)
    {
        Original = original;
        LocalizationSet = localizationSet;
        Culture = culture;
    }
}
