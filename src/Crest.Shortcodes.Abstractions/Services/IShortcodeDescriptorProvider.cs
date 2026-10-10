namespace Crest.Shortcodes.Services;

public interface IShortcodeDescriptorProvider
{
    Task<IEnumerable<ShortcodeDescriptor>> DiscoverAsync();
}
