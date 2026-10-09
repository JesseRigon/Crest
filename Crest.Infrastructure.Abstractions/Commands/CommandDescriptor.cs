using System.Reflection;

namespace Crest.Environment.Commands;

public class CommandDescriptor
{
    public string[] Names { get; set; }
    public MethodInfo MethodInfo { get; set; }
    public string HelpText { get; set; }
}
