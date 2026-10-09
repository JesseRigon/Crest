using Crest.ContentLocalization.Handlers;
using Crest.ContentLocalization.Models;
using Crest.ContentManagement;
using Crest.Lists.Indexes;
using Crest.Lists.Models;
using YesSql;

namespace Crest.Lists.Drivers;

public class ListPartLocalizationHandler : ContentLocalizationPartHandlerBase<ListPart>
{
    private readonly ISession _session;

    public ListPartLocalizationHandler(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Select Contained ContentItems that are already in the target culture
    /// but attached to the original list and reassign their ListContenItemId.
    /// </summary>
    public override async Task LocalizedAsync(LocalizationContentContext context, ListPart part)
    {
        var containedList = await _session.Query<ContentItem, ContainedPartIndex>(
            x => x.ListContentItemId == context.Original.ContentItemId).ListAsync();

        if (!containedList.Any())
        {
            return;
        }

        foreach (var item in containedList)
        {
            if (item.TryGet<LocalizationPart>(out var localizationPart) &&
                localizationPart.Culture == context.Culture &&
                item.TryGet<ContainedPart>(out var containedPart))
            {
                containedPart.ListContentItemId = context.ContentItem.ContentItemId;
                containedPart.Apply();
                await _session.SaveAsync(item);
            }
        }
    }
}
