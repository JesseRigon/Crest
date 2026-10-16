using System.Text;
using Grove.Data.Connectors;
using MailKit.Net.Smtp;
using MimeKit;

namespace Grove.Data.Protocols.Smtp;

/// <summary>
/// The SMTP protocol of the connector engine (<see cref="ConnectorProtocols.Smtp"/>): one
/// operation, <see cref="SendOperation"/>, that hands a MIME message to the connection's server
/// with MailKit. The engine resolves the credential and applies the policy; this makes one
/// attempt and classifies its failure. Everything that fails before the server accepted the
/// message's envelope (DNS, connect, TLS, sign-in, a refused sender or recipient) is reported
/// as not sent, so the engine may retry it even though sending is unsafe to repeat.
/// </summary>
public sealed class SmtpConnectorProtocol : IConnectorProtocol
{
    /// <summary>The raw request method of a send: the request body is the MIME message.</summary>
    public const string SendMethod = "SEND";

    public const string MessageContentType = "message/rfc822";

    /// <summary>The operation kind of an SMTP send.</summary>
    public const string SendOperationKind = "smtp-send";

    public const int DefaultPort = 587;

    public static readonly ConnectionOperation SendOperation = new()
    {
        Id = "send",
        DisplayName = "Send a message",
        Description = "Sends one MIME message (the request body) to the connection's SMTP server.",
        Kind = SendOperationKind,
        Method = SendMethod,
        ContentType = MessageContentType,
        IsSafeToRetry = false,
    };

    public string Id => ConnectorProtocols.Smtp;

    public string DisplayName => "SMTP";

    public IReadOnlyList<string> AuthSchemes { get; } = [Connectors.AuthSchemes.None, Connectors.AuthSchemes.Basic];

    public IReadOnlyList<string> SchemaFormats { get; } = [];

    public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SchemaReadResult([SendOperation]));

    public async Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var mime = context.Request?.Body ?? context.Arguments?["message"]?.GetValue<string>();
        if (string.IsNullOrEmpty(mime))
        {
            return ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), "The request carries no message to send.");
        }

        MimeMessage message;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mime));
            message = await MimeMessage.LoadAsync(stream, cancellationToken);
        }
        catch (FormatException exception)
        {
            return ProtocolResponse.Failed(new FailureClassification(FailureKind.Refused, NotSent: true), $"The message is not a valid MIME message: {exception.Message}");
        }

        using var client = new SmtpClient();
        var handedOver = false;
        try
        {
            var endpoint = MailServerEndpoint.From(context.Connection, DefaultPort);
            await MailServerConnection.ConnectAsync(client, endpoint, context, cancellationToken);
            await MailServerConnection.AuthenticateAsync(client, context.Credential, cancellationToken);

            handedOver = true;
            var response = await client.SendAsync(message, cancellationToken);

            try
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The server accepted the message; a failed QUIT changes nothing.
            }

            return new ProtocolResponse { StatusCode = 250, Body = response };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var classification = handedOver ? ClassifySending(exception) : MailServerConnection.ClassifyBeforeSend(exception);
            var status = exception is SmtpCommandException command ? (int)command.StatusCode : 0;

            return ProtocolResponse.Failed(classification, exception.Message, status);
        }
    }

    public FailureClassification Classify(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        CredentialUnreadableException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        SmtpCommandException command => ClassifySending(command),
        _ => new FailureClassification(FailureKind.Transient),
    };

    /// <summary>
    /// A failure during the mail transaction. A command the server refused (sender, a recipient,
    /// the message itself) proves the message was not taken: 4xx is worth retrying, 5xx is not.
    /// A broken connection in the middle leaves it unknown whether the server took the message.
    /// </summary>
    public static FailureClassification ClassifySending(Exception exception) => exception switch
    {
        SmtpCommandException command when (int)command.StatusCode is >= 400 and < 500
            => new FailureClassification(FailureKind.Transient, NotSent: true),
        SmtpCommandException => new FailureClassification(FailureKind.Permanent, NotSent: true),
        _ => new FailureClassification(FailureKind.Transient),
    };
}
