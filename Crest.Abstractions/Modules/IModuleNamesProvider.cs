namespace Crest.Modules;

public interface IModuleNamesProvider
{
    IEnumerable<string> GetModuleNames();
}
