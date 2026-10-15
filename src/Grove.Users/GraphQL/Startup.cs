using Microsoft.Extensions.DependencyInjection;
using Grove.Apis.GraphQL;
using Grove.Modules;

namespace Grove.Users.GraphQL;

[RequireFeatures("Grove.Apis.GraphQL", "Grove.Contents")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, CurrentUserQuery>();
        services.AddTransient<UserType>();
    }
}
