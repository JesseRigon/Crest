using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Rules.Models;

namespace Crest.Rules.Drivers;

public sealed class IsAuthenticatedConditionDisplayDriver : DisplayDriver<Condition, IsAuthenticatedCondition>
{
    public override Task<IDisplayResult> DisplayAsync(IsAuthenticatedCondition condition, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("IsAuthenticatedCondition_Fields_Summary", condition).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("IsAuthenticatedCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(IsAuthenticatedCondition condition, BuildEditorContext context)
    {
        return View("IsAuthenticatedCondition_Fields_Edit", condition).Location("Content");
    }
}
