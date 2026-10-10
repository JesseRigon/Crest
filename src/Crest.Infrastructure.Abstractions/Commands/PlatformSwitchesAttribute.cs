namespace Crest.Environment.Commands;

[AttributeUsage(AttributeTargets.Method)]
public class PlatformSwitchesAttribute : Attribute
{
    private readonly string _switches;

    public PlatformSwitchesAttribute(string switches)
    {
        _switches = switches;
    }

    public IEnumerable<string> Switches
    {
        get
        {
            return (_switches ?? "").Trim().Split(',').Select(s => s.Trim());
        }
    }
}
