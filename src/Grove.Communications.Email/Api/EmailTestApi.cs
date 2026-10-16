using Grove.Api;
using Grove.Communications.Delivery;
using Grove.Communications.Email.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Communications.Email.Api;

/// <summary>An enabled email provider and the connection it uses (null for one that needs none).</summary>
public sealed record EmailProviderModel(string Name, string DisplayName, string? Connection = null);

/// <summary>The e-mail channel on the Communications page: its send and receive providers and defaults.</summary>
public sealed record EmailChannelModel(
    string? DefaultProvider,
    IReadOnlyList<EmailProviderModel> Providers,
    string? DefaultReceiveProvider,
    IReadOnlyList<EmailProviderModel> ReceiveProviders);

/// <param name="DefaultProvider">The provider messages go through by default; null for none.</param>
public sealed record EmailProvidersModel(string? DefaultProvider, IReadOnlyList<EmailProviderModel> Providers);

/// <summary>A test message; an empty <see cref="Provider"/> sends through the default.</summary>
public sealed record EmailTestRequest(string? Provider, string To, string? From, string? Cc, string? Bcc, string? ReplyTo, string? Subject, string? Body);

public sealed record EmailTestResult(bool Succeeded, IReadOnlyList<string> Errors);

/// <summary>
/// <c>api/grove/email</c>: the enabled e-mail providers and a test send, for the Email Test page.
/// Both need <c>ManageEmailSettings</c>. The channel's settings are the <c>email</c> settings group.
/// </summary>
public sealed class EmailTestApi : ApiContributor
{
    public override string Name => "EmailTest";

    public override string FeatureId => "Grove.Communications.Email";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("email");
        routes.MapGet("providers", ApiHandlers.Handle<EmailTestHandlers>(h => h.ListProvidersAsync()));
        routes.MapGet("channel", ApiHandlers.Handle<EmailTestHandlers>(h => h.ChannelAsync()));
        routes.MapPost("test", (HttpContext http, [FromBody] EmailTestRequest request) => ApiHandlers.For<EmailTestHandlers>(http).TestAsync(request));
    }
}

public sealed class EmailTestHandlers(
    IAuthorizationService authorization,
    IOptionsMonitor<EmailOptions> emailOptions,
    IOptionsMonitor<EmailProviderOptions> providerOptions,
    IOptionsMonitor<EmailReceiveProviderOptions> receiveProviderOptions,
    IEmailProviderResolver providerResolver,
    IEmailReceiveProviderResolver receiveProviderResolver,
    IEmailService emailService) : ApiHandlers
{
    public async Task<IResult> ListProvidersAsync()
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        return Ok(new EmailProvidersModel(emailOptions.CurrentValue.DefaultProviderName, await SendProvidersAsync()));
    }

    /// <summary>The channel's card on the Communications page.</summary>
    public async Task<IResult> ChannelAsync()
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        var receive = new List<EmailProviderModel>();
        foreach (var entry in receiveProviderOptions.CurrentValue.Providers.Where(entry => entry.Value.IsEnabled))
        {
            var provider = await receiveProviderResolver.GetAsync(entry.Key);
            receive.Add(new EmailProviderModel(entry.Key, provider.DisplayName.Value, (provider as IConnectedProvider)?.ConnectionKey));
        }

        var options = emailOptions.CurrentValue;
        return Ok(new EmailChannelModel(
            options.DefaultProviderName,
            await SendProvidersAsync(),
            options.DefaultReceiveProviderName,
            [.. receive.OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)]));
    }

    private async Task<IReadOnlyList<EmailProviderModel>> SendProvidersAsync()
    {
        var list = new List<EmailProviderModel>();
        foreach (var entry in providerOptions.CurrentValue.Providers.Where(entry => entry.Value.IsEnabled))
        {
            var provider = await providerResolver.GetAsync(entry.Key);
            list.Add(new EmailProviderModel(entry.Key, provider.DisplayName.Value, (provider as IConnectedProvider)?.ConnectionKey));
        }

        return [.. list.OrderBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Sends a test message (Tools › Testing › Email Test).</summary>
    public async Task<IResult> TestAsync([FromBody] EmailTestRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.To))
        {
            return ApiProblems.Validation("to", "A recipient is required.");
        }

        var message = new MailMessage
        {
            To = request.To,
            Cc = request.Cc,
            Bcc = request.Bcc,
            ReplyTo = request.ReplyTo,
            From = string.IsNullOrWhiteSpace(request.From) ? null : request.From,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject,
            TextBody = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body,
        };

        try
        {
            var result = await emailService.SendAsync(message, string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider, HttpContext.RequestAborted);
            return Ok(new EmailTestResult(result.Succeeded, [.. result.Errors.Select(error => $"{error.Key}: {error.Message.Value}".TrimStart(':', ' '))]));
        }
        catch (InvalidEmailProviderException)
        {
            return Ok(new EmailTestResult(false, ["The selected provider is invalid or no longer enabled."]));
        }
    }
}
