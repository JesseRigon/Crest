using Crest.Environment.Extensions;

namespace Crest.DisplayManagement.Theming;

public interface IThemeManager
{
    Task<IExtensionInfo> GetThemeAsync();
}
