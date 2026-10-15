using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.UrlRewriting.Handlers;
using Grove.UrlRewriting.Services;

namespace Grove.UrlRewriting.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUrlRewritingServices(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<RewriteOptions>, RewriteOptionsConfiguration>();
        services.AddSingleton<IRewriteRulesStore, RewriteRulesStore>();
        services.AddScoped<IRewriteRulesManager, RewriteRulesManager>();
        services.AddScoped<IRewriteRuleHandler, RewriteRuleHandler>();

        return services;
    }
}
