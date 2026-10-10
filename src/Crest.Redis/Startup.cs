using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Caching.Distributed;
using Crest.Environment.Cache;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Locking.Distributed;
using Crest.Modules;
using Crest.Redis.Options;
using Crest.Redis.Services;
using StackExchange.Redis;

namespace Crest.Redis;

public sealed class Startup : StartupBase
{
    private readonly string _tenant;
    private readonly IShellConfiguration _configuration;
    private readonly ILogger _logger;

    public Startup(ShellSettings shellSettings, IShellConfiguration configuration, ILogger<Startup> logger)
    {
        _tenant = shellSettings.Name;
        _configuration = configuration;
        _logger = logger;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        try
        {
            var section = _configuration.GetSection("Crest_Redis");

            var configuration = section["Configuration"];
            var configurationOptions = ConfigurationOptions.Parse(configuration);
            var instancePrefix = section["InstancePrefix"];

            if (section.GetValue("DisableCertificateVerification", false))
            {
                configurationOptions.CertificateValidation += IgnoreCertificateErrors;
            }

            var allowAdmin = section.GetValue<bool?>("AllowAdmin", null);
            if (allowAdmin.HasValue)
            {
                configurationOptions.AllowAdmin = allowAdmin.Value;
            }

            services.Configure<RedisOptions>(options =>
            {
                options.Configuration = configuration;
                options.ConfigurationOptions = configurationOptions;
                options.InstancePrefix = instancePrefix;
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "'Redis' features are not active on tenant '{TenantName}' as the 'Configuration' string is missing or invalid.", _tenant);
            return;
        }

        services.AddSingleton<IRedisService, RedisService>();
        services.AddSingleton<IModularTenantEvents>(sp => sp.GetRequiredService<IRedisService>());
        services.AddSingleton<IRedisDatabaseFactory, RedisDatabaseFactory>();
    }

    // Callback for accepting any certificate as long as it exists, while ignoring other SSL policy errors.
    // This allows the use of self-signed certificates on the Redis server.
    private static bool IgnoreCertificateErrors(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        => (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) == 0;
}

[Feature("Crest.Redis.Cache")]
public sealed class RedisCacheStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IRedisService)))
        {
            services.AddSingleton<IDistributedCache, RedisCacheWrapper>(sp =>
            {
                var optionsAccessor = sp.GetRequiredService<IOptions<RedisCacheOptions>>();
                return new RedisCacheWrapper(new RedisCache(optionsAccessor));
            });
            services.AddTransient<IConfigureOptions<RedisCacheOptions>, RedisCacheOptionsSetup>();
            services.AddScoped<ITagCache, RedisTagCache>();
        }
    }
}

[Feature("Crest.Redis.Bus")]
public sealed class RedisBusStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IRedisService)))
        {
            services.AddSingleton<IMessageBus, RedisBus>();
        }
    }
}

[Feature("Crest.Redis.Lock")]
public sealed class RedisLockStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IRedisService)))
        {
            services.AddSingleton<IDistributedLock, RedisLock>();
        }
    }
}

[Feature("Crest.Redis.DataProtection")]
public sealed class RedisDataProtectionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IRedisService)))
        {
            services.AddTransient<IConfigureOptions<KeyManagementOptions>, RedisKeyManagementOptionsSetup>();
        }
    }
}
