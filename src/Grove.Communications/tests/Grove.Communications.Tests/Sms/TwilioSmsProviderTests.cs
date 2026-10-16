using System.Text.Json.Nodes;
using Grove.Communications.Sms;
using Grove.Data.Connectors;
using Grove.Integrations.Twilio;
using Grove.Integrations.Twilio.Models;
using Grove.Integrations.Twilio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Grove.Communications.Tests.Sms;

public class TwilioSmsProviderTests
{
    private static readonly TwilioOptions s_options = new()
    {
        IsEnabled = true,
        PhoneNumber = "+15550001111",
        AccountSID = "AC123",
        ConnectionKey = TwilioConnection.Key,
    };

    [Fact]
    public async Task Send_creates_a_message_on_the_connection_with_an_idempotency_token()
    {
        var engine = new FakeConnectorEngine
        {
            Respond = _ => FakeConnectorEngine.Json(new JsonObject { ["sid"] = "SM1", ["status"] = "queued" }, 201),
        };

        var result = await CreateProvider(engine, s_options).SendAsync(new SmsMessage { To = "+15552223333", Body = "Hello" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.Calls);
        Assert.Equal(TwilioConnection.Key, call.ConnectionKey);
        Assert.Same(Callers.System, call.Caller);
        Assert.Equal(TwilioConnection.CreateMessage.Id, call.OperationId);
        Assert.False(string.IsNullOrEmpty(call.IdempotencyKey));
        Assert.Equal("POST", call.Request.Method);
        Assert.Equal("/2010-04-01/Accounts/AC123/Messages.json", call.Request.Path);
        Assert.Equal(TwilioConnection.FormContentType, call.Request.ContentType);
        Assert.Contains("To=%2B15552223333", call.Request.Body);
        Assert.Contains("From=%2B15550001111", call.Request.Body);
        Assert.Equal("AC123", call.Arguments["AccountSid"]!.GetValue<string>());
    }

    [Fact]
    public async Task Each_send_has_its_own_idempotency_token()
    {
        var engine = new FakeConnectorEngine
        {
            Respond = _ => FakeConnectorEngine.Json(new JsonObject { ["status"] = "accepted" }, 201),
        };
        var provider = CreateProvider(engine, s_options);

        await provider.SendAsync(new SmsMessage { To = "+15552223333", Body = "One" }, TestContext.Current.CancellationToken);
        await provider.SendAsync(new SmsMessage { To = "+15552223333", Body = "Two" }, TestContext.Current.CancellationToken);

        Assert.NotEqual(engine.Calls[0].IdempotencyKey, engine.Calls[1].IdempotencyKey);
    }

    [Fact]
    public async Task A_failed_message_status_or_call_is_a_failure()
    {
        var failedStatus = new FakeConnectorEngine
        {
            Respond = _ => FakeConnectorEngine.Json(new JsonObject { ["status"] = "failed", ["error_code"] = 30003 }, 201),
        };
        var failedCall = new FakeConnectorEngine
        {
            Respond = _ => new ConnectorResult { Outcome = ConnectorOutcome.Failed, StatusCode = 400, Body = """{"code":21211,"message":"Invalid 'To' Phone Number"}""" },
        };

        Assert.False((await CreateProvider(failedStatus, s_options).SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await CreateProvider(failedCall, s_options).SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task A_disabled_provider_calls_nothing()
    {
        var engine = new FakeConnectorEngine();

        var result = await CreateProvider(engine, new TwilioOptions { IsEnabled = false, AccountSID = "AC123" })
            .SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public void The_connection_is_rest_with_a_basic_tenant_credential_and_an_idempotent_unsafe_send()
    {
        var builder = new ConnectionBuilder(TwilioConnection.Key);
        TwilioConnection.Declare(builder);
        var connection = builder.Build();

        Assert.Equal(ConnectorProtocols.Rest, connection.Protocol);
        Assert.Equal(AuthSchemes.Basic, connection.Auth.Scheme);
        Assert.Equal([CredentialScope.Tenant], connection.AllowedScopes);
        Assert.Equal(TwilioConnection.FeatureId, connection.FeatureId);

        var create = Assert.Single(connection.Operations, operation => operation.Id == TwilioConnection.CreateMessage.Id);
        Assert.False(create.IsSafeToRetry);
        Assert.True(create.SupportsIdempotencyKey);
        Assert.Equal(TwilioConnection.IdempotencyHeader, create.IdempotencyKeyHeader);
        Assert.All(connection.Operations.Where(operation => operation.Method == "GET"), operation => Assert.True(operation.IsSafeToRetry));
    }

    private static TwilioSmsProvider CreateProvider(FakeConnectorEngine engine, TwilioOptions options) => new(
        Monitors.Of(options),
        engine,
        Callers.Factory(),
        NullLogger<TwilioSmsProvider>.Instance,
        new PassThroughLocalizer<TwilioSmsProvider>());
}
