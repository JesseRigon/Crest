using Crest.Queries;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Queries;

/// <summary>
/// The one run-query activity (docs/operations.md step 3): every query the catalog lists is
/// an engine descriptor whose CLR type is this class, with the query's parameters as
/// synthetic inputs and its columns as synthetic outputs
/// (<see cref="QueryActivityProvider"/>). Executes the query through the query pipeline as
/// the burst's caller (the activity gate decided <c>ExecuteApi_{query}</c> for it first);
/// outputs the page's rows, the next page token and, per column, the first row's value.
/// </summary>
[Activity("Crest.Workflows", "Queries", "Runs a query of the registry with the flow's caller and outputs its rows.", DisplayName = "Run query")]
public class RunQuery : Activity
{
    /// <summary>The query this node runs; set by the descriptor's constructor, not by the author.</summary>
    public string QueryName { get; set; } = string.Empty;

    [Input(DisplayName = "Page token", Description = "The token a previous run handed back as Next page token; empty for the first page.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> PageToken { get; set; } = null!;

    [Input(DisplayName = "Page size", Description = "Overrides the query's page size when smaller than the query's own.", UIHint = InputUIHints.SingleLine)]
    public Input<int?> PageSize { get; set; } = null!;

    [Output(Description = "The rows of the page.")]
    public Output<IList<object>> Rows { get; set; } = null!;

    [Output(Description = "The token for the next page; empty on the last page.")]
    public Output<string?> NextPageToken { get; set; } = null!;

    [Output(Description = "The number of rows across all pages, when the source can tell.")]
    public Output<long?> Total { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var descriptor = context.ActivityDescriptor;
        var queryName = QueryName is { Length: > 0 } ? QueryName : descriptor.CustomProperties.TryGetValue(WorkflowsConstants.QueryNameDescriptorProperty, out var named) ? named?.ToString() ?? string.Empty : string.Empty;
        if (queryName.Length == 0)
        {
            throw new InvalidOperationException("The run-query activity names no query; it is created from a query descriptor, never by hand.");
        }

        var parameters = new Dictionary<string, object>();
        foreach (var input in descriptor.Inputs.Where(input => input.IsSynthetic))
        {
            var reference = SyntheticProperties.TryGetValue(input.Name, out var value) ? value as Input : null;
            var evaluated = reference is not null ? context.Get(reference.MemoryBlockReference()) : null;
            if (evaluated is not null)
            {
                parameters[QueryActivityProvider.ParameterName(input)] = evaluated;
            }
        }

        var queryManager = context.GetRequiredService<IQueryManager>();
        var query = await queryManager.GetQueryAsync(queryName)
            ?? new Query { Name = queryName, Source = descriptor.CustomProperties.TryGetValue(QueryActivityProvider.SourceProperty, out var source) ? source?.ToString() ?? string.Empty : string.Empty };
        var results = await queryManager.ExecuteQueryAsync(query, new QueryRequest
        {
            Parameters = parameters,
            PageToken = PageToken.GetOrDefault(context) is { Length: > 0 } token ? token : null,
            PageSize = PageSize.GetOrDefault(context),
            CancellationToken = context.CancellationToken,
        });

        var rows = results.Items.ToList();
        Rows.Set(context, rows);
        NextPageToken.Set(context, results.NextPageToken);
        Total.Set(context, results.Total);

        var first = rows.FirstOrDefault();
        foreach (var output in descriptor.Outputs.Where(output => output.IsSynthetic))
        {
            if (SyntheticProperties.TryGetValue(output.Name, out var value) && value is Output target)
            {
                context.Set(target, first is null ? null : ColumnValue(first, QueryActivityProvider.ParameterName(output)), output.Name);
            }
        }

        context.JournalData["Query"] = queryName;
        context.JournalData["Rows"] = rows.Count;
        await context.CompleteActivityAsync();
    }

    /// <summary>A column of a row: a dictionary row by key, a JSON row by property, a typed row by property.</summary>
    public static object? ColumnValue(object row, string column) => row switch
    {
        IDictionary<string, object> dictionary => dictionary.FirstOrDefault(pair => string.Equals(pair.Key, column, StringComparison.OrdinalIgnoreCase)).Value,
        System.Text.Json.Nodes.JsonObject node => node.FirstOrDefault(pair => string.Equals(pair.Key, column, StringComparison.OrdinalIgnoreCase)).Value,
        System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Object => element.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, column, StringComparison.OrdinalIgnoreCase)).Value,
        _ => row.GetType().GetProperty(column, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.GetValue(row),
    };
}

/// <summary>
/// Emits one <see cref="ActivityDescriptor"/> per query of <see cref="IQueryCatalog"/>: the
/// query's parameters as typed synthetic inputs, its columns as typed synthetic outputs, the
/// output schema under <see cref="WorkflowsConstants.QuerySchemaDescriptorProperty"/>, and
/// the query's execute permission under the gate's key, so the activity gate decides it per
/// query. The CLR type is always <see cref="RunQuery"/>; the designer's palette, the
/// descriptors API and the options endpoint read these like any other activity.
/// </summary>
public sealed class QueryActivityProvider(IQueryCatalog catalog, IActivityDescriber describer) : IActivityProvider
{
    public const string TypeNamePrefix = "Crest.Queries.";
    public const string SourceProperty = "crest:querySource";
    private const string SyntheticPrefix = "Query_";

    public async ValueTask<IEnumerable<ActivityDescriptor>> GetDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        var queries = await catalog.ListAsync(cancellationToken);
        if (queries.Count == 0)
        {
            return [];
        }

        var template = await describer.DescribeActivityAsync(typeof(RunQuery), cancellationToken);
        return queries.Select(query => Describe(query, template)).ToList();
    }

    public static ActivityDescriptor Describe(QueryDescriptor query, ActivityDescriptor template)
    {
        var typeName = TypeNamePrefix + SafeName(query.Name);
        var inputs = template.Inputs.ToList();
        inputs.AddRange(query.Parameters.Select(parameter => new InputDescriptor
        {
            Name = SyntheticName(parameter.Name),
            DisplayName = parameter.Name,
            Description = parameter.Required ? "Required." : null,
            Type = parameter.ClrType,
            IsWrapped = true,
            IsSynthetic = true,
            UIHint = parameter.ClrType == typeof(bool) ? InputUIHints.Checkbox : InputUIHints.SingleLine,
            Category = "Parameters",
            ValueGetter = activity => activity.SyntheticProperties.TryGetValue(SyntheticName(parameter.Name), out var value) ? value : null,
            ValueSetter = (activity, value) => activity.SyntheticProperties[SyntheticName(parameter.Name)] = value!,
        }));

        var outputs = template.Outputs.ToList();
        outputs.AddRange(query.Columns.Select(column => new OutputDescriptor
        {
            Name = SyntheticName(column.Name),
            DisplayName = column.Name,
            Description = "The first row's value.",
            Type = column.Type,
            IsSynthetic = true,
            ValueGetter = activity => activity.SyntheticProperties.TryGetValue(SyntheticName(column.Name), out var value) ? value : null,
            ValueSetter = (activity, value) => activity.SyntheticProperties[SyntheticName(column.Name)] = value!,
        }));

        return new ActivityDescriptor
        {
            TypeName = typeName,
            ClrType = typeof(RunQuery),
            Namespace = "Crest.Queries",
            Name = SafeName(query.Name),
            Version = 1,
            DisplayName = query.DisplayName,
            Description = $"Runs the query '{query.DisplayName}' ({query.Source}): the page on Rows, each column output the first row's value.",
            Category = "Queries",
            Kind = ActivityKind.Action,
            Inputs = inputs,
            Outputs = outputs,
            Attributes = template.Attributes,
            Ports = template.Ports,
            IsBrowsable = true,
            CustomProperties =
            {
                [WorkflowsConstants.QueryNameDescriptorProperty] = query.Name,
                [SourceProperty] = query.Source,
                [WorkflowsConstants.RequiredPermissionDescriptorProperty] = QueryPermissions.CreatePermissionForQuery(query.Name).Name,
                [WorkflowsConstants.QuerySchemaDescriptorProperty] = new Dictionary<string, object>
                {
                    ["source"] = query.Source,
                    ["pageable"] = true,
                    // Lookup semantics: a column output carries the first row's value; the page is on Rows.
                    ["columnOutputs"] = "first-row",
                    ["columns"] = query.Columns.Select(column => new Dictionary<string, object> { ["name"] = column.Name, ["type"] = column.Type.Name }).ToList(),
                },
            },
            ConstructionProperties = new Dictionary<string, object> { [nameof(RunQuery.QueryName)] = query.Name },
            Constructor = context =>
            {
                var result = context.CreateActivity<RunQuery>();
                result.Activity.Type = typeName;
                result.Activity.QueryName = query.Name;
                return result;
            },
        };
    }

    /// <summary>The parameter or column a synthetic input or output stands for.</summary>
    public static string ParameterName(PropertyDescriptor descriptor) => descriptor.Name.StartsWith(SyntheticPrefix, StringComparison.Ordinal) ? descriptor.Name[SyntheticPrefix.Length..] : descriptor.Name;

    // Prefixed so a parameter named like one of the class's own properties (PageSize, Rows) never collides.
    private static string SyntheticName(string name) => SyntheticPrefix + name;

    private static string SafeName(string name) => new(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
}
