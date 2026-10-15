using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Contents.Services;

namespace Grove.Contents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentServices(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<MvcOptions>, MvcOptionsConfiguration>();

        return services;
    }
}
