using System.Reflection;

namespace Crest.Environment.Extensions;

public class ExtensionEntry
{
    public IExtensionInfo ExtensionInfo { get; set; }
    public Assembly Assembly { get; set; }
    public IEnumerable<Type> ExportedTypes { get; set; }
    public bool IsError { get; set; }
}
