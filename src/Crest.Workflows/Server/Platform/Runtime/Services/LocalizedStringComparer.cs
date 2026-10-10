using Microsoft.Extensions.Localization;

namespace Crest.Workflows.Platform.Services;

public class LocalizedStringComparer : IEqualityComparer<LocalizedString>
{
    public bool Equals(LocalizedString x, LocalizedString y)
    {
        return x.Name.Equals(y.Name, StringComparison.Ordinal);
    }

    public int GetHashCode(LocalizedString obj)
    {
        return obj.Name.GetHashCode();
    }
}
