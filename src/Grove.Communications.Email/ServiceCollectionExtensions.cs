using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Communications.Email.Services;

namespace Grove.Communications.Email;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services.AddScoped<IEmailService, DefaultEmailService>();
        services.AddScoped<IEmailProviderResolver, DefaultEmailProviderResolver>();
        services.AddScoped<IEmailServiceEvents, EmailMessageValidator>();
        services.AddScoped<IEmailReceiveService, DefaultEmailReceiveService>();
        services.AddScoped<IEmailReceiveProviderResolver, DefaultEmailReceiveProviderResolver>();

        services.AddTransient<IConfigureOptions<EmailOptions>, EmailOptionsConfiguration>();

        return services;
    }

    public static IServiceCollection AddEmailProvider<T>(this IServiceCollection services, string name)
        where T : class, IEmailProvider
    {
        services.Configure<EmailProviderOptions>(options =>
        {
            options.TryAddProvider(name, new EmailProviderTypeOptions(typeof(T))
            {
                IsEnabled = true,
            });
        });

        return services;
    }

    public static IServiceCollection AddEmailProviderOptionsConfiguration<TConfiguration>(this IServiceCollection services)
        where TConfiguration : class, IConfigureOptions<EmailProviderOptions>
    {
        services.AddTransient<IConfigureOptions<EmailProviderOptions>, TConfiguration>();

        return services;
    }

    /// <summary>Registers a receive provider configuration (the receive registry's contributor).</summary>
    public static IServiceCollection AddEmailReceiveProviderOptionsConfiguration<TConfiguration>(this IServiceCollection services)
        where TConfiguration : class, IConfigureOptions<EmailReceiveProviderOptions>
    {
        services.AddTransient<IConfigureOptions<EmailReceiveProviderOptions>, TConfiguration>();

        return services;
    }
}
