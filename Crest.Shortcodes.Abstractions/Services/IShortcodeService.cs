using Shortcodes;

namespace Crest.Shortcodes.Services;

public interface IShortcodeService
{
    ValueTask<string> ProcessAsync(string input, Context context = null);
}
