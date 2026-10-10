using Dapper;
using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Fluid;
using Fluid.Values;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Crest.Workflows.Queries.UI;
using Crest.Liquid;
using Crest.Queries.Sql;
using YesSql;

namespace Crest.Workflows.Queries.Activities;

[Activity("Crest.Queries", "Queries", "Executes a SQL query and returns the results.", DisplayName = "Run SQL Query")]
[UsedImplicitly]
public class RunSqlQuery : CodeActivity<ICollection<dynamic>>
{
    [Input(
        Description = "The SQL query to execute.",
        UIHint = InputUIHints.CodeEditor,
        UIHandler = typeof(SqlCodeOptionsProvider)
    )]
    public Input<string> Query { get; set; } = null!;

    [Input(
        Description = "An optional dictionary of parameters to pass to the query.",
        UIHint = InputUIHints.Dictionary
    )]
    public Input<IDictionary<string, object>> Parameters { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var store = context.GetRequiredService<IStore>();
        var liquidTemplateManager = context.GetRequiredService<ILiquidTemplateManager>();
        var templateOptions = context.GetRequiredService<IOptions<TemplateOptions>>().Value;
        var query = Query.Get(context);
        var parameters = Parameters.GetOrDefault(context) ?? new Dictionary<string, object>();
        var dialect = store.Configuration.SqlDialect;
        var tokenizedQuery = await liquidTemplateManager.RenderStringAsync(query, NullEncoder.Default, parameters.Select(x => new KeyValuePair<string, FluidValue>(x.Key, FluidValue.Create(x.Value, templateOptions))));

        if (SqlParser.TryParse(tokenizedQuery, store.Configuration.Schema, dialect, store.Configuration.TablePrefix, parameters, out var rawQuery, out var messages))
        {
            await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync();
            var documents = await connection.QueryAsync(rawQuery, parameters);

            context.SetResult(documents);
        }
        else
        {
            throw new SqlParserException(string.Join(", ", messages));
        }
    }
}