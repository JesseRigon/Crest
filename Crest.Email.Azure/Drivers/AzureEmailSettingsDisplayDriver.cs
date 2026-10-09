using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Email;
using Crest.Email.Azure;
using Crest.Email.Azure.Models;
using Crest.Email.Azure.Services;
using Crest.Email.Azure.ViewModels;
using Crest.Email.Services;
using Crest.Entities;
using Crest.Environment.Options;
using Crest.Mvc.ModelBinding;
using Crest.Settings;

namespace Crest.Azure.Email.Drivers;

public sealed class AzureEmailSettingsDisplayDriver : SiteDisplayDriver<AzureEmailSettings>
{
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IEmailAddressValidator _emailValidator;

    internal readonly IStringLocalizer S;

    public AzureEmailSettingsDisplayDriver(
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        IEmailAddressValidator emailValidator,
        IStringLocalizer<AzureEmailSettingsDisplayDriver> stringLocalizer)
    {
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _emailValidator = emailValidator;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => EmailSettings.GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, AzureEmailSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, EmailPermissions.ManageEmailSettings))
        {
            return null;
        }

        return Initialize<AzureEmailSettingsViewModel>("AzureEmailSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.DefaultSender = settings.DefaultSender;
            model.ConnectionString = settings.ConnectionString;
        }).Location("Content:5#Azure Communication Services")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AzureEmailSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, EmailPermissions.ManageEmailSettings))
        {
            return null;
        }

        var model = new AzureEmailSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var emailSettings = site.GetOrCreate<EmailSettings>();

        var hasChanges = model.IsEnabled != settings.IsEnabled;

        settings.IsEnabled = model.IsEnabled;

        if (!model.IsEnabled)
        {
            if (hasChanges && emailSettings.DefaultProviderName == AzureEmailProvider.TechnicalName)
            {
                emailSettings.DefaultProviderName = null;

                site.Put(emailSettings);
            }
        }
        else
        {
            hasChanges |= model.DefaultSender != settings.DefaultSender;

            if (string.IsNullOrEmpty(model.DefaultSender))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultSender), S["The Default Sender is a required field."]);
            }
            else if (!_emailValidator.Validate(model.DefaultSender))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultSender), S["The Default Sender is invalid."]);
            }

            settings.DefaultSender = model.DefaultSender;

            if (string.IsNullOrWhiteSpace(model.ConnectionString))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionString), S["Connection string is required."]);
            }
            else
            {
                if (model.ConnectionString != settings.ConnectionString)
                {
                    // Encrypt the connection string.
                    var protector = _dataProtectionProvider.CreateProtector(AzureEmailOptionsConfiguration.ProtectorName);

                    var protectedConnection = protector.Protect(model.ConnectionString);

                    // Check if the connection string changed before setting it.
                    hasChanges |= protectedConnection != settings.ConnectionString;

                    settings.ConnectionString = protectedConnection;
                }
            }
        }

        if (context.Updater.ModelState.IsValid)
        {
            if (settings.IsEnabled && string.IsNullOrEmpty(emailSettings.DefaultProviderName))
            {
                // If we are enabling the only provider, set it as the default one.
                emailSettings.DefaultProviderName = AzureEmailProvider.TechnicalName;
                site.Put(emailSettings);

                hasChanges = true;
            }

            if (hasChanges)
            {
                _optionsUpdateNotifier
                    .RequestUpdate<AzureEmailOptions>()
                    .RequestUpdate<EmailProviderOptions>()
                    .RequestUpdate<EmailOptions>();
            }
        }

        return await EditAsync(site, settings, context);
    }
}
