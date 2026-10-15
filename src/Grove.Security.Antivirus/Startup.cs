using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Grove.Security.Antivirus.ClamAV;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Storage;
using Grove.Modules;

namespace Grove.Security.Antivirus;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }
}

[Feature("Grove.Security.Antivirus.ClamAV")]
public sealed class ClamAVStartup : StartupBase
{
    private readonly IShellConfiguration _configuration;
    private readonly ILogger _logger;

    public ClamAVStartup(IShellConfiguration configuration, ILogger<ClamAVStartup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        var section = _configuration.GetSection(ClamAvOptions.ConfigSection);

        // Enabled without a scanner host is an expected state (a tenant enables every feature in
        // dev): one warning, and no scanning handler is registered, so uploads are not scanned
        // rather than all failing. A configured but unreachable scanner still fails closed.
        if (string.IsNullOrWhiteSpace(section[nameof(ClamAvOptions.Host)]))
        {
            _logger.LogWarning("The ClamAV antivirus scanner is enabled but not active because '{Section}:Host' is not configured; uploads are not scanned.", ClamAvOptions.ConfigSection);
            return;
        }

        services.Configure<ClamAvOptions>(section);
        services.TryAddSingleton<ClamAvConnectionFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFileEventHandler, ClamAvFileEventHandler>());
    }
}
