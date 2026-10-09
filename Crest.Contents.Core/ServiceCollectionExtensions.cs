using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Contents.Core.Services;

namespace Crest.Contents.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentServices(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<MvcOptions>, MvcOptionsConfiguration>();

        return services;
    }
}
