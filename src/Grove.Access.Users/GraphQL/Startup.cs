using Microsoft.Extensions.DependencyInjection;
using Grove.Data.Protocols.GraphQL;
using Grove.Modules;

namespace Grove.Access.Users.GraphQL;

[RequireFeatures("Grove.Data.Protocols.GraphQL", "Grove.Contents")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, CurrentUserQuery>();
        services.AddTransient<UserType>();
    }
}
