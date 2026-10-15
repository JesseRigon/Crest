using Grove.Environment.Extensions;

namespace Grove.Display.Management.Theming;

public interface IThemeManager
{
    Task<IExtensionInfo> GetThemeAsync();
}
