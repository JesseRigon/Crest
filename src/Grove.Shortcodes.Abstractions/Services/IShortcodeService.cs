using Shortcodes;

namespace Grove.Shortcodes.Services;

public interface IShortcodeService
{
    ValueTask<string> ProcessAsync(string input, Context context = null);
}
