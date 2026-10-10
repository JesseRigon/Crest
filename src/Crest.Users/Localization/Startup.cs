using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Users.Localization.Drivers;
using Crest.Users.Localization.Providers;
using Crest.Users.Models;
using Crest.Users.Services;

namespace Crest.Users.Localization;

[Feature("Crest.Users.Localization")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<User, UserLocalizationDisplayDriver>();
        services.AddScoped<IUserClaimsProvider, UserLocalizationClaimsProvider>();

        services.Configure<RequestLocalizationOptions>(options =>
            options.AddInitialRequestCultureProvider(new UserLocalizationRequestCultureProvider()));
    }
}
