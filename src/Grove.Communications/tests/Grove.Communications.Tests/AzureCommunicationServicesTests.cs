using System.Net.Http;
using Azure;
using Azure.Communication.Sms;
using Grove.Communications.Email;
using Grove.Communications.Sms;
using Grove.Data.Connectors;
using Grove.Integrations.Microsoft;
using Grove.Integrations.Microsoft.Email;
using Grove.Integrations.Microsoft.Sms;
using Grove.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grove.Communications.Tests;

public class AzureCommunicationServicesTests
{
    [Fact]
    public async Task Email_runs_the_sdk_send_in_the_engine_as_an_unsafe_call()
    {
        var engine = new FakeConnectorEngine { SdkResult = "operation-id" };
        var provider = new AzureEmailProvider(
            Monitors.Of(new AzureEmailOptions { IsEnabled = true, DefaultSender = "noreply@example.com", ConnectionKey = "acs-mail" }),
            engine,
            Callers.Factory(),
            NullLogger<AzureEmailProvider>.Instance,
            new PassThroughLocalizer<AzureEmailProvider>());

        var result = await provider.SendAsync(new MailMessage { To = "someone@example.com", Subject = "Hi", TextBody = "Body" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.SdkCalls);
        Assert.Equal("acs-mail", call.ConnectionKey);
        Assert.Same(Callers.System, call.Caller);
        Assert.False(call.Options.IsSafeToRetry);
        Assert.NotNull(call.Options.Classify);
    }

    [Fact]
    public async Task Email_reports_a_failed_sdk_call()
    {
        var engine = new FakeConnectorEngine { SdkOutcome = ConnectorOutcome.Failed };
        var provider = new AzureEmailProvider(
            Monitors.Of(new AzureEmailOptions { IsEnabled = true, DefaultSender = "noreply@example.com" }),
            engine,
            Callers.Factory(),
            NullLogger<AzureEmailProvider>.Instance,
            new PassThroughLocalizer<AzureEmailProvider>());

        var result = await provider.SendAsync(new MailMessage { To = "someone@example.com", TextBody = "Body" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AzureCommunicationServices.EmailConnectionKey, Assert.Single(engine.SdkCalls).ConnectionKey);
    }

    [Fact]
    public async Task Sms_runs_the_sdk_send_in_the_engine_and_reads_its_result()
    {
        var engine = new FakeConnectorEngine
        {
            SdkResult = Azure.Communication.Sms.Models.SmsModelFactory.SmsSendResult("+15552223333", "message-id", 202, successful: true, errorMessage: null),
        };
        var phone = Substitute.For<IPhoneFormatValidator>();
        phone.IsValid(Arg.Any<string>()).Returns(true);
        var provider = new AzureSmsProvider(
            Monitors.Of(new AzureSmsOptions { IsEnabled = true, PhoneNumber = "+15550001111", ConnectionKey = "acs-sms" }),
            engine,
            Callers.Factory(),
            phone,
            NullLogger<AzureSmsProvider>.Instance,
            new PassThroughLocalizer<AzureSmsProvider>());

        var result = await provider.SendAsync(new SmsMessage { To = "+15552223333", Body = "Hi" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var call = Assert.Single(engine.SdkCalls);
        Assert.Equal("acs-sms", call.ConnectionKey);
        Assert.False(call.Options.IsSafeToRetry);
    }

    [Fact]
    public void The_connections_run_the_sdk_with_a_custom_tenant_credential()
    {
        foreach (var (key, declare, feature) in new (string, Action<ConnectionBuilder>, string)[]
        {
            (AzureCommunicationServices.EmailConnectionKey, AzureCommunicationServices.DeclareEmail, AzureCommunicationServices.Features.Email),
            (AzureCommunicationServices.SmsConnectionKey, AzureCommunicationServices.DeclareSms, AzureCommunicationServices.Features.Sms),
        })
        {
            var builder = new ConnectionBuilder(key);
            declare(builder);
            var connection = builder.Build();

            Assert.Equal(ConnectorProtocols.Sdk, connection.Protocol);
            Assert.Equal(AuthSchemes.Custom, connection.Auth.Scheme);
            Assert.Equal([CredentialScope.Tenant], connection.AllowedScopes);
            Assert.Equal(feature, connection.FeatureId);
            Assert.All(connection.Operations, operation => Assert.False(operation.IsSafeToRetry));
        }
    }

    [Fact]
    public void Refusals_and_unreached_services_are_not_sent_and_server_errors_are_unknown()
    {
        Assert.Equal(new FailureClassification(FailureKind.Unauthorized, NotSent: true), AzureCommunicationServices.Classify(new RequestFailedException(401, "unauthorized")));
        Assert.Equal(new FailureClassification(FailureKind.Permanent, NotSent: true), AzureCommunicationServices.Classify(new RequestFailedException(400, "bad request")));
        Assert.Equal(FailureKind.RateLimited, AzureCommunicationServices.Classify(new RequestFailedException(429, "slow down")).Kind);
        Assert.True(AzureCommunicationServices.Classify(new RequestFailedException(429, "slow down")).NotSent);
        Assert.Equal(new FailureClassification(FailureKind.Transient), AzureCommunicationServices.Classify(new RequestFailedException(503, "unavailable")));
        Assert.Equal(
            new FailureClassification(FailureKind.Transient, NotSent: true),
            AzureCommunicationServices.Classify(new HttpRequestException(HttpRequestError.ConnectionError, "refused")));
        Assert.Equal(new FailureClassification(FailureKind.Transient), AzureCommunicationServices.Classify(new HttpRequestException(HttpRequestError.ResponseEnded, "cut")));
    }
}
