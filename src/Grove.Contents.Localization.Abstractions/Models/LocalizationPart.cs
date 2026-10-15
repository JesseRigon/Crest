using Grove.Contents.Management;

namespace Grove.Contents.Localization.Models;

public class LocalizationPart : ContentPart, ILocalizable
{
    public string LocalizationSet { get; set; }
    public string Culture { get; set; }
}
