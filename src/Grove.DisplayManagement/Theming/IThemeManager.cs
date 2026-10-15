using Grove.Environment.Extensions;

namespace Grove.DisplayManagement.Theming;

public interface IThemeManager
{
    Task<IExtensionInfo> GetThemeAsync();
}
