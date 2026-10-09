using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Rules.Models;

namespace Crest.Rules.Drivers;

public sealed class IsAnonymousConditionDisplayDriver : DisplayDriver<Condition, IsAnonymousCondition>
{
    public override Task<IDisplayResult> DisplayAsync(IsAnonymousCondition condition, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("IsAnonymousCondition_Fields_Summary", condition).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("IsAnonymousCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(IsAnonymousCondition condition, BuildEditorContext context)
    {
        return View("IsAnonymousCondition_Fields_Edit", condition).Location("Content");
    }
}
