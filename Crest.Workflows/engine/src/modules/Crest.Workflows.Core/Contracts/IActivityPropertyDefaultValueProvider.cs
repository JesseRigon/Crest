using System.Reflection;

namespace Crest.Workflows;

public interface IActivityPropertyDefaultValueProvider
{
    object GetDefaultValue(PropertyInfo property);
}