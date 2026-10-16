using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp;
using Grove.Data.Protocols.Smtp.Receiving;
using MailKit;
using MailKit.Net.Pop3;

namespace Grove.Data.Protocols.Pop;

/// <summary>
/// The POP3 protocol of the connector engine (<see cref="ConnectorProtocols.Pop"/>): reads the
/// maildrop's messages after a cursor (the UIDL of the last message read) and removes messages
/// by UIDL. Needs a server with UIDL; one attempt per call.
/// </summary>
public sealed class PopConnectorProtocol : IConnectorProtocol
{
    public const int DefaultPort = 995;

    public string Id => ConnectorProtocols.Pop;

    public string DisplayName => "POP3";

    public IReadOnlyList<string> AuthSchemes { get; } = [Connectors.AuthSchemes.None, Connectors.AuthSchemes.Basic];

    public IReadOnlyList<string> SchemaFormats { get; } = [];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SchemaReadResult([Mailbox.FetchOperation, Mailbox.DeleteOperation]));

    public async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.Request?.Method ?? context.Operation?.Method;
        if (method is not (Mailbox.FetchMethod or Mailbox.DeleteMethod))
        {
            return ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), $"POP3 has no '{method}' operation.");
        }

        using var client = new Pop3Client();
        var connected = false;
        try
        {
            var endpoint = MailServerEndpoint.From(context.Connection, DefaultPort);
            await MailServerConnection.ConnectAsync(client, endpoint, context, cancellationToken);
            await MailServerConnection.AuthenticateAsync(client, context.Credential, cancellationToken);
            connected = true;

            if (!client.Capabilities.HasFlag(Pop3Capabilities.UIDL))
            {
                return ProtocolResponse.Failed(new FailureClassification(FailureKind.Permanent, NotSent: true), "The POP3 server does not support UIDL, which reading by cursor needs.");
            }

            var uids = await client.GetMessageUidsAsync(cancellationToken);
            var response = method == Mailbox.FetchMethod
                ? await FetchAsync(client, uids, context, cancellationToken)
                : await DeleteAsync(client, uids, context, cancellationToken);

            // QUIT commits the deletions.
            await client.DisconnectAsync(true, cancellationToken);

            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var classification = connected
                ? method == Mailbox.FetchMethod
                    ? Classify(exception)
                    : new FailureClassification(exception is CommandException ? FailureKind.Permanent : FailureKind.Transient)
                : MailServerConnection.ClassifyBeforeSend(exception);

            return ProtocolResponse.Failed(classification, exception.Message);
        }
    }

    public FailureClassification Classify(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        CredentialUnreadableException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        CommandException => new FailureClassification(FailureKind.Permanent),
        _ => new FailureClassification(FailureKind.Transient),
    };

    private static async Task<ProtocolResponse> FetchAsync(Pop3Client client, IList<string> uids, IProtocolCallContext context, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(context.Arguments?[Mailbox.Max]?.GetValue<int>() ?? 50, 1, 500);
        var cursor = context.Arguments?[Mailbox.Cursor]?.GetValue<string>();

        // The maildrop lists oldest first; read after the last message read (from the start when it is gone).
        var start = cursor is null ? 0 : uids.IndexOf(cursor) + 1;

        var messages = new List<(string, byte[])>();
        long size = 0;
        var last = cursor;
        for (var index = start; index < uids.Count && messages.Count < max; index++)
        {
            var bytes = await Mailbox.ToBytesAsync(await client.GetMessageAsync(index, cancellationToken), cancellationToken);
            size += (bytes.Length + 2) / 3 * 4;
            if (size > context.MaxResponseBytes)
            {
                if (messages.Count == 0)
                {
                    throw new ConnectorRefusedException($"The message {uids[index]} is larger than the connection's response cap.");
                }

                break;
            }

            messages.Add((uids[index], bytes));
            last = uids[index];
        }

        var hasMore = start + messages.Count < uids.Count;

        return new ProtocolResponse { StatusCode = 200, Value = Mailbox.FetchResponse(messages, last, hasMore) };
    }

    private static async Task<ProtocolResponse> DeleteAsync(Pop3Client client, IList<string> uids, IProtocolCallContext context, CancellationToken cancellationToken)
    {
        var ids = (context.Arguments?[Mailbox.Ids]?.AsArray() ?? [])
            .Select(node => node?.GetValue<string>())
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        var indexes = uids.Select((uid, index) => (uid, index)).Where(entry => ids.Contains(entry.uid)).Select(entry => entry.index).ToList();
        if (indexes.Count > 0)
        {
            await client.DeleteMessagesAsync(indexes, cancellationToken);
        }

        return new ProtocolResponse { StatusCode = 200 };
    }
}
