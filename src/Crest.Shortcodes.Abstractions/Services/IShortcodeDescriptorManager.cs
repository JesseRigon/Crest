namespace Crest.Shortcodes.Services;

public interface IShortcodeDescriptorManager
{
    Task<IEnumerable<ShortcodeDescriptor>> GetShortcodeDescriptors();
}
