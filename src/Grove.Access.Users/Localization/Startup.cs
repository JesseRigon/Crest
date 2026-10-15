using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Access.Users.Localization.Providers;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;

namespace Grove.Access.Users.Localization;

[Feature("Grove.Access.Users.Localization")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserClaimsProvider, UserLocalizationClaimsProvider>();

        services.Configure<RequestLocalizationOptions>(options =>
            options.AddInitialRequestCultureProvider(new UserLocalizationRequestCultureProvider()));
    }
}
