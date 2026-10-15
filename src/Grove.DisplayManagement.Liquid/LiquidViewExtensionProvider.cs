using Grove.DisplayManagement.Razor;

namespace Grove.DisplayManagement.Liquid;

public class LiquidViewExtensionProvider : IRazorViewExtensionProvider
{
    public string ViewExtension => LiquidViewTemplate.ViewExtension;
}
