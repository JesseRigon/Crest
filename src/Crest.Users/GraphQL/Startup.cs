using Microsoft.Extensions.DependencyInjection;
using Crest.Apis.GraphQL;
using Crest.Modules;

namespace Crest.Users.GraphQL;

[RequireFeatures("Crest.Apis.GraphQL", "Crest.Contents")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISchemaBuilder, CurrentUserQuery>();
        services.AddTransient<UserType>();
    }
}
