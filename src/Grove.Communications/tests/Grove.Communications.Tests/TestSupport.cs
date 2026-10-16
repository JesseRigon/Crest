using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace Grove.Communications.Tests;

/// <summary>A localizer that returns the format, filled in.</summary>
public sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

/// <summary>
/// A connector engine that records what it is asked to run and answers with what the test set:
/// the providers under test are thin, so what they hand the engine is what they do.
/// </summary>
public sealed class FakeConnectorEngine : IConnectorEngine
{
    public List<ConnectorCall> Calls { get; } = [];

    public List<(string ConnectionKey, CallerContext Caller, SdkCallOptions Options)> SdkCalls { get; } = [];

    public Func<ConnectorCall, ConnectorResult> Respond { get; set; } = _ => new ConnectorResult { Outcome = ConnectorOutcome.Succeeded, StatusCode = 200 };

    /// <summary>The value an SDK call returns; null runs nothing and answers failed.</summary>
    public object SdkResult { get; set; }

    public ConnectorOutcome SdkOutcome { get; set; } = ConnectorOutcome.Succeeded;

    public Task<ConnectorResult> ExecuteAsync(ConnectorCall call, CancellationToken cancellationToken = default)
    {
        Calls.Add(call);
        return Task.FromResult(Respond(call));
    }

    public Task<ConnectorResult<T>> RunSdkAsync<T>(
        string connectionKey,
        CallerContext caller,
        Func<ISdkCallContext, CancellationToken, Task<T>> call,
        SdkCallOptions options = null,
        CancellationToken cancellationToken = default)
    {
        SdkCalls.Add((connectionKey, caller, options));

        return Task.FromResult(new ConnectorResult<T>
        {
            Outcome = SdkOutcome,
            Result = SdkResult is T value ? value : default,
        });
    }

    public static ConnectorResult Json(JsonNode value, int status = 200) =>
        new() { Outcome = ConnectorOutcome.Succeeded, StatusCode = status, Value = value, Body = value.ToJsonString() };
}

public static class Callers
{
    public static readonly CallerContext System = new()
    {
        Tenant = "Default",
        Side = CallerSide.System,
        IsSystem = true,
        IsSuperUser = true,
    };

    /// <summary>A factory whose system actor is <see cref="System"/>.</summary>
    public static ICallerContextFactory Factory()
    {
        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateSystemAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(System);
        return factory;
    }
}

public static class Monitors
{
    public static Microsoft.Extensions.Options.IOptionsMonitor<T> Of<T>(T value)
    {
        var monitor = Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<T>>();
        monitor.CurrentValue.Returns(value);
        return monitor;
    }
}
