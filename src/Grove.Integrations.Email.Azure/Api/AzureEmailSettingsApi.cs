#nullable enable

using Grove.Api;
using Grove.Integrations.Email.Azure.Models;
using Grove.Integrations.Email.Azure.Services;
using Grove.Communications.Email.Services;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Communications.Email;

namespace Grove.Integrations.Email.Azure.Api;

/// <summary>
/// Settings › Email › Azure Communication Services (the native email settings page's provider
/// section): the same rules the retired settings driver applied. The connection string is
/// write-only: <see cref="AzureEmailSettingsModel.HasConnectionString"/> says whether one is stored.
/// </summary>
public sealed class AzureEmailSettingsApi : ApiContributor
{
    public override string Name => "AzureEmailSettings";

    public override string FeatureId => "Grove.Integrations.Email.Azure";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/email/azure");
        routes.MapGet("", ApiHandlers.Handle<AzureEmailSettingsHandlers>(handlers => handlers.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] AzureEmailSettingsUpdate update) => ApiHandlers.For<AzureEmailSettingsHandlers>(__http).PutAsync(update));
    }
}

public sealed class AzureEmailSettingsHandlers(
    IAuthorizationService authorization,
    ISiteService siteService,
    IDataProtectionProvider dataProtectionProvider,
    IEmailAddressValidator emailValidator,
    IOptionsUpdateNotifier optionsUpdateNotifier) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        return Ok(ToModel((await siteService.GetSiteSettingsAsync()).GetOrCreate<AzureEmailSettings>()));
    }

    public async Task<IResult> PutAsync([FromBody] AzureEmailSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, EmailPermissions.ManageEmailSettings))
        {
            return Forbid();
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<AzureEmailSettings>();
        var emailSettings = site.GetOrCreate<EmailSettings>();

        if (update.IsEnabled)
        {
            var errors = new ValidationErrors();
            if (string.IsNullOrEmpty(update.DefaultSender))
            {
                errors.Add(nameof(update.DefaultSender), "The Default Sender is a required field.");
            }
            else if (!emailValidator.Validate(update.DefaultSender))
            {
                errors.Add(nameof(update.DefaultSender), "The Default Sender is invalid.");
            }

            if (string.IsNullOrWhiteSpace(update.ConnectionString) && string.IsNullOrEmpty(settings.ConnectionString))
            {
                errors.Add(nameof(update.ConnectionString), "Connection string is required.");
            }

            if (!errors.IsEmpty)
            {
                return BadRequest(errors);
            }
        }

        settings.IsEnabled = update.IsEnabled;
        if (!update.IsEnabled)
        {
            if (emailSettings.DefaultProviderName == AzureEmailProvider.TechnicalName)
            {
                emailSettings.DefaultProviderName = null;
            }
        }
        else
        {
            settings.DefaultSender = update.DefaultSender;
            if (!string.IsNullOrWhiteSpace(update.ConnectionString))
            {
                settings.ConnectionString = dataProtectionProvider.CreateProtector(AzureEmailOptionsConfiguration.ProtectorName).Protect(update.ConnectionString);
            }

            // Enabling the only provider makes it the default.
            if (string.IsNullOrEmpty(emailSettings.DefaultProviderName))
            {
                emailSettings.DefaultProviderName = AzureEmailProvider.TechnicalName;
            }
        }

        site.Put(settings);
        site.Put(emailSettings);
        await siteService.UpdateSiteSettingsAsync(site);
        optionsUpdateNotifier
            .RequestUpdate<AzureEmailOptions>()
            .RequestUpdate<EmailProviderOptions>()
            .RequestUpdate<EmailOptions>();

        return Ok(ToModel(settings));
    }

    private static AzureEmailSettingsModel ToModel(AzureEmailSettings settings) =>
        new(settings.IsEnabled, settings.DefaultSender, !string.IsNullOrEmpty(settings.ConnectionString));
}

public sealed record AzureEmailSettingsModel(bool IsEnabled, string? DefaultSender, bool HasConnectionString);

/// <param name="ConnectionString">A new connection string; empty keeps the stored one.</param>
public sealed record AzureEmailSettingsUpdate(bool IsEnabled, string? DefaultSender, string? ConnectionString);
