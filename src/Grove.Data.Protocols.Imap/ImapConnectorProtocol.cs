using System.Globalization;
using Grove.Data.Connectors;
using Grove.Data.Protocols.Smtp;
using Grove.Data.Protocols.Smtp.Receiving;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;

namespace Grove.Data.Protocols.Imap;

/// <summary>
/// The IMAP protocol of the connector engine (<see cref="ConnectorProtocols.Imap"/>): reads a
/// folder's messages after a cursor (<c>{UIDVALIDITY}:{last UID}</c>) without marking them read,
/// and removes messages by UID. One attempt per call; the engine owns credentials and policy.
/// </summary>
public sealed class ImapConnectorProtocol : IConnectorProtocol
{
    public const int DefaultPort = 993;

    public const string DefaultFolder = "INBOX";

    public string Id => ConnectorProtocols.Imap;

    public string DisplayName => "IMAP";

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
            return ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), $"IMAP has no '{method}' operation.");
        }

        var folderName = context.Arguments?[Mailbox.Folder]?.GetValue<string>()
            ?? context.Connection.Setting(MailConnectionSettings.Folder)
            ?? DefaultFolder;

        using var client = new ImapClient();
        var connected = false;
        try
        {
            var endpoint = MailServerEndpoint.From(context.Connection, DefaultPort);
            await MailServerConnection.ConnectAsync(client, endpoint, context, cancellationToken);
            await MailServerConnection.AuthenticateAsync(client, context.Credential, cancellationToken);
            connected = true;

            var folder = await client.GetFolderAsync(folderName, cancellationToken);
            var response = method == Mailbox.FetchMethod
                ? await FetchAsync(folder, context, cancellationToken)
                : await DeleteAsync(folder, context, cancellationToken);

            await client.DisconnectAsync(true, cancellationToken);

            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var classification = connected
                ? method == Mailbox.FetchMethod
                    ? Classify(exception)
                    : new FailureClassification(exception is CommandException or FolderNotFoundException ? FailureKind.Permanent : FailureKind.Transient)
                : MailServerConnection.ClassifyBeforeSend(exception);

            return ProtocolResponse.Failed(classification, exception.Message);
        }
    }

    public FailureClassification Classify(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        CredentialUnreadableException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        FolderNotFoundException or CommandException => new FailureClassification(FailureKind.Permanent),
        _ => new FailureClassification(FailureKind.Transient),
    };

    private static async Task<ProtocolResponse> FetchAsync(IMailFolder folder, IProtocolCallContext context, CancellationToken cancellationToken)
    {
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var max = Math.Clamp(context.Arguments?[Mailbox.Max]?.GetValue<int>() ?? 50, 1, 500);
        var (validity, lastUid) = ReadCursor(context.Arguments?[Mailbox.Cursor]?.GetValue<string>());
        if (validity != folder.UidValidity)
        {
            // A new UIDVALIDITY renumbers the folder: start over.
            lastUid = 0;
        }

        var uids = (await folder.SearchAsync(SearchQuery.All, cancellationToken))
            .Where(uid => uid.Id > lastUid)
            .OrderBy(uid => uid.Id)
            .ToList();

        var messages = new List<(string, byte[])>();
        long size = 0;
        var cursorUid = lastUid;
        foreach (var uid in uids.Take(max))
        {
            var bytes = await Mailbox.ToBytesAsync(await folder.GetMessageAsync(uid, cancellationToken), cancellationToken);
            size += (bytes.Length + 2) / 3 * 4;
            if (size > context.MaxResponseBytes)
            {
                if (messages.Count == 0)
                {
                    throw new ConnectorRefusedException($"The message {uid.Id} is larger than the connection's response cap.");
                }

                break;
            }

            messages.Add((uid.Id.ToString(CultureInfo.InvariantCulture), bytes));
            cursorUid = uid.Id;
        }

        var hasMore = uids.Count > messages.Count;
        var cursor = string.Create(CultureInfo.InvariantCulture, $"{folder.UidValidity}:{cursorUid}");

        return new ProtocolResponse { StatusCode = 200, Value = Mailbox.FetchResponse(messages, cursor, hasMore) };
    }

    private static async Task<ProtocolResponse> DeleteAsync(IMailFolder folder, IProtocolCallContext context, CancellationToken cancellationToken)
    {
        var ids = (context.Arguments?[Mailbox.Ids]?.AsArray() ?? [])
            .Select(node => uint.TryParse(node?.GetValue<string>(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? new UniqueId(id) : UniqueId.Invalid)
            .Where(uid => uid.IsValid)
            .ToList();

        if (ids.Count > 0)
        {
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
            await folder.AddFlagsAsync(ids, MessageFlags.Deleted, silent: true, cancellationToken);
            await folder.ExpungeAsync(ids, cancellationToken);
        }

        return new ProtocolResponse { StatusCode = 200 };
    }

    private static (uint Validity, uint LastUid) ReadCursor(string? cursor)
    {
        var parts = cursor?.Split(':');
        return parts is { Length: 2 }
            && uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var validity)
            && uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            ? (validity, last)
            : (0, 0);
    }
}
