using Grove.Access;
using Grove.Communications.Delivery;
using Grove.Communications.Email;
using Grove.Data.Connectors;
using Grove.Infrastructure;
using Microsoft.Extensions.Localization;

namespace Grove.Data.Protocols.Smtp.Receiving;

/// <summary>Which mailbox a receive provider reads: its connection and folder.</summary>
public sealed record MailboxTarget(bool IsEnabled, string? ConnectionKey, string? Folder);

/// <summary>
/// A thin receive provider over a mailbox connection (<c>imap</c>, <c>pop</c>): asks the
/// connector engine to read or remove messages as the tenant's system actor, and turns the
/// answer into the channel's received messages. Holds no credential.
/// </summary>
public abstract class MailboxReceiveProvider : IEmailReceiveProvider, IConnectedProvider
{
    private readonly IConnectorEngine _engine;
    private readonly ICallerContextFactory _callers;

    protected MailboxReceiveProvider(IConnectorEngine engine, ICallerContextFactory callers, IStringLocalizer stringLocalizer)
    {
        _engine = engine;
        _callers = callers;
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    public abstract LocalizedString DisplayName { get; }

    public string? ConnectionKey => Target.ConnectionKey;

    /// <summary>The provider's current connection and folder.</summary>
    protected abstract MailboxTarget Target { get; }

    public async Task<Result<EmailReceiveBatch>> ReceiveAsync(EmailReceiveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var target = Target;
        if (!target.IsEnabled || string.IsNullOrEmpty(target.ConnectionKey))
        {
            return Result.Failed<EmailReceiveBatch>(S["The receive provider is disabled or has no connection."]);
        }

        var result = await RunAsync(target.ConnectionKey, Mailbox.FetchMethod, Mailbox.FetchArguments(request.Folder ?? target.Folder, request.Cursor, Math.Clamp(request.MaxMessages, 1, 500)), cancellationToken);
        if (!result.Succeeded)
        {
            return Result.Failed<EmailReceiveBatch>(Describe(result));
        }

        return Result.Success(Mailbox.ReadBatch(result.Value, request.Cursor));
    }

    public async Task<Result> DeleteAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        var target = Target;
        if (!target.IsEnabled || string.IsNullOrEmpty(target.ConnectionKey))
        {
            return Result.Failed(S["The receive provider is disabled or has no connection."]);
        }

        if (messageIds.Count == 0)
        {
            return Result.Success();
        }

        var result = await RunAsync(target.ConnectionKey, Mailbox.DeleteMethod, Mailbox.DeleteArguments(target.Folder, messageIds), cancellationToken);

        return result.Succeeded ? Result.Success() : Result.Failed(Describe(result));
    }

    private async Task<ConnectorResult> RunAsync(string connectionKey, string method, System.Text.Json.Nodes.JsonObject arguments, CancellationToken cancellationToken)
    {
        var caller = await _callers.CreateSystemAsync(cancellationToken: cancellationToken);

        return await _engine.ExecuteAsync(new ConnectorCall
        {
            ConnectionKey = connectionKey,
            Caller = caller,
            Arguments = arguments,
            Request = new ConnectorRequest(method, null),
        }, cancellationToken);
    }

    private LocalizedString Describe(ConnectorResult result) => result.Outcome switch
    {
        ConnectorOutcome.NotAuthorized => S["The mailbox connection has no usable credential."],
        ConnectorOutcome.RetryAfter => S["The mail server asked to try again later."],
        _ => S["The mailbox could not be read: '{0}'", result.Error ?? result.Outcome.ToString()],
    };
}
