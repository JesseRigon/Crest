using System.Reflection;
using System.Runtime.CompilerServices;
using Grove.Workflows.Extensions;
using Grove.Workflows;
using Grove.Workflows.Attributes;
using Grove.Workflows.UIHints;
using Grove.Workflows.Models;

namespace Grove.Workflows.Http;

/// <summary>
/// Send an HTTP request.
/// </summary>
[Activity("Grove.Workflows", "HTTP", "Send an HTTP request.", DisplayName = "HTTP Request (flow)", Kind = ActivityKind.Task)]
public class FlowSendHttpRequest : SendHttpRequestBase, IActivityPropertyDefaultValueProvider
{
    /// <inheritdoc />
    public FlowSendHttpRequest([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <summary>
    /// A list of expected status codes to handle.
    /// </summary>
    [Input(
        Description = "A list of expected status codes to handle.",
        UIHint = InputUIHints.MultiText,
        DefaultValueProvider = typeof(FlowSendHttpRequest),
        Order = 5.1f
    )]
    public Input<ICollection<int>> ExpectedStatusCodes { get; set; } = null!;

    /// <inheritdoc />
    protected override async ValueTask HandleResponseAsync(ActivityExecutionContext context, HttpResponseMessage response)
    {
        var expectedStatusCodes = ExpectedStatusCodes.GetOrDefault(context) ?? new List<int>(0);
        var statusCode = (int)response.StatusCode;
        var hasMatchingStatusCode = expectedStatusCodes.Contains(statusCode);
        var outcome = expectedStatusCodes.Any() ? hasMatchingStatusCode ? statusCode.ToString() : "Unmatched status code" : null;
        var outcomes = new List<string>();

        if (outcome != null)
            outcomes.Add(outcome);

        outcomes.Add("Done");
        await context.CompleteActivityWithOutcomesAsync(outcomes.ToArray());
    }

    /// <inheritdoc />
    protected override async ValueTask HandleRequestExceptionAsync(ActivityExecutionContext context, HttpRequestException exception)
    {
        await context.CompleteActivityWithOutcomesAsync("Failed to connect");
    }

    /// <inheritdoc />
    protected override async ValueTask HandleTaskCanceledExceptionAsync(ActivityExecutionContext context, TaskCanceledException exception)
    {
        await context.CompleteActivityWithOutcomesAsync("Timeout");
    }

    object IActivityPropertyDefaultValueProvider.GetDefaultValue(PropertyInfo property)
    {
        if (property.Name == nameof(ExpectedStatusCodes))
        {
            return new List<int> { 200 };
        }

        return null!;
    }
}