using Crest.DisplayManagement.Razor;

namespace Crest.DisplayManagement.Liquid;

public class LiquidViewExtensionProvider : IRazorViewExtensionProvider
{
    public string ViewExtension => LiquidViewTemplate.ViewExtension;
}
