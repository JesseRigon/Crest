using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Shortcodes.Drivers;

public sealed class ShortcodeDescriptorDisplayDriver : DisplayDriver<ShortcodeDescriptor>
{
    public override Task<IDisplayResult> DisplayAsync(ShortcodeDescriptor descriptor, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ShortcodeDescriptor_Fields_SummaryAdmin", descriptor).Location(PlatformConstants.DisplayType.SummaryAdmin, "Content"),
            View("ShortcodeDescriptor_SummaryAdmin__Button__Actions", descriptor).Location(PlatformConstants.DisplayType.SummaryAdmin, "Actions")
        );
    }
}
