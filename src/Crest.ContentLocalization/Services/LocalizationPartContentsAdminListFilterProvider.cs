using Crest.ContentLocalization.Records;
using Crest.ContentLocalization.ViewModels;
using Crest.ContentManagement;
using Crest.Contents.Services;
using YesSql.Filters.Query;

namespace Crest.ContentLocalization.Services;

public sealed class LocalizationPartContentsAdminListFilterProvider : IContentsAdminListFilterProvider
{
    public void Build(QueryEngineBuilder<ContentItem> builder)
    {
        builder
            .WithNamedTerm("culture", builder => builder
                .OneCondition<ContentItem>((val, query) =>
                {
                    if (!string.IsNullOrEmpty(val))
                    {
                        var normalized = val.ToLowerInvariant();
                        query.With<LocalizedContentItemIndex>(i => (i.Published || i.Latest) && i.Culture == normalized);
                    }

                    return query;
                })
                .MapTo<LocalizationContentsAdminFilterViewModel>((val, model) => model.SelectedCulture = val)
                .MapFrom<LocalizationContentsAdminFilterViewModel>((model) =>
                {
                    if (!string.IsNullOrEmpty(model.SelectedCulture))
                    {
                        return (true, model.SelectedCulture);
                    }
                    return (false, string.Empty);
                })
            );
    }
}
