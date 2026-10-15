using System.Reflection;

namespace Grove.Workflows;

public interface IActivityPropertyDefaultValueProvider
{
    object GetDefaultValue(PropertyInfo property);
}