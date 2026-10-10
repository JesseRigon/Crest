using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Entities;
using Crest.Mvc.ModelBinding;
using Crest.Queries.Sql.Models;
using Crest.Queries.Sql.ViewModels;

namespace Crest.Queries.Sql.Drivers;

public sealed class SqlQueryDisplayDriver : DisplayDriver<Query>
{
    internal readonly IStringLocalizer S;

    public SqlQueryDisplayDriver(IStringLocalizer<SqlQueryDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Display(Query query, BuildDisplayContext context)
    {
        if (query.Source != SqlQuerySource.SourceName)
        {
            return null;
        }

        return Combine(
            Dynamic("SqlQuery_SummaryAdmin", static (model, query) =>
            {
                model.Query = query;
            }, query).Location("Content:5"),
            Dynamic("SqlQuery_Buttons_SummaryAdmin", static (model, query) =>
            {
                model.Query = query;
            }, query).Location("Actions:2")
        );
    }

    public override async Task<IDisplayResult> EditAsync(Query query, BuildEditorContext context)
    {
        if (query.Source != SqlQuerySource.SourceName)
        {
            return null;
        }

        var template = string.Empty;
        if (query.TryGet<SqlQueryMetadata>(out var metadata))
        {
            template = metadata.Template;
        }

        // Extract query from the query string if we come from the main query editor.
        // Create model object here, to make sure that TryUpdateModelAsync work on a specific object type, not over a proxied one
        var viewModel = new SqlQueryViewModel();
        if (string.IsNullOrEmpty(template))
        {
            await context.Updater.TryUpdateModelAsync(viewModel, string.Empty, m => m.Query);
            template = viewModel.Query;
        }

        return Initialize<SqlQueryViewModel>("SqlQuery_Edit", model =>
        {
            model.ReturnDocuments = query.ReturnContentItems;
            model.Query = template;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(Query query, UpdateEditorContext context)
    {
        if (query.Source != SqlQuerySource.SourceName)
        {
            return null;
        }

        var viewModel = new SqlQueryViewModel();
        await context.Updater.TryUpdateModelAsync(viewModel, Prefix,
            m => m.Query,
            m => m.ReturnDocuments);

        if (string.IsNullOrWhiteSpace(viewModel.Query))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Query), S["The query field is required"]);
        }
        else
        {
            // Liquid is rejected here, at save: the template is parsed as written.
            foreach (var message in SqlParser.Validate(viewModel.Query))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Query), message);
            }
        }

        query.ReturnContentItems = viewModel.ReturnDocuments;
        query.Put(new SqlQueryMetadata()
        {
            Template = viewModel.Query,
        });

        return await EditAsync(query, context);
    }
}
