using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Diagnostics;

/// <summary>
/// The status code pages and the exception handler re-execute "/Error/{status}" (and "/Error"),
/// which each shell answers with its own error page (Grove.Components' GroveErrorPage; step 7:
/// the MVC Diagnostics controller and views are retired). The re-execution runs inside the shell
/// that answered the original request, so its shell stamp and base path still hold.
/// </summary>
public sealed class Startup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, DiagnosticsStartupFilter>();
    }
}
