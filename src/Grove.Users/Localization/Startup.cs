using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Users.Localization.Providers;
using Grove.Users.Models;
using Grove.Users.Services;

namespace Grove.Users.Localization;

[Feature("Grove.Users.Localization")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserClaimsProvider, UserLocalizationClaimsProvider>();

        services.Configure<RequestLocalizationOptions>(options =>
            options.AddInitialRequestCultureProvider(new UserLocalizationRequestCultureProvider()));
    }
}
