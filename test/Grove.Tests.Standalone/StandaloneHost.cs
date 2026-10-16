using Grove.Environment.Shell;
using Grove.Environment.Shell.Scope;
using Grove.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Grove.Tests.Standalone;

/// <summary>
/// A host built the way a product host is (AddPlatformCms, the host defaults, UsePlatform),
/// referencing Grove's application layer and nothing downstream, installed fresh: its own
/// data root under the temp folder, the Default tenant set up by AutoSetup from
/// <c>Recipes/grove.standalone.recipe.json</c> on the first request. One instance for the
/// whole test class; the data root is deleted afterwards.
/// </summary>
public sealed class StandaloneHost : IAsyncLifetime
{
    public const string AdminUserName = "admin";
    public const string RecipeName = "GroveStandalone";

    private WebApplication? _app;

    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "grove-standalone-" + Guid.NewGuid().ToString("N"));

    public HttpClient Client { get; private set; } = default!;

    public IShellHost ShellHost => _app!.Services.GetRequiredService<IShellHost>();

    /// <summary>The host's own services (the extension manager, the shell host).</summary>
    public IServiceProvider Services => _app!.Services;

    public async ValueTask InitializeAsync()
    {
        // A web root of the instance's own (media caches write under it); the build output has none.
        var webRoot = Path.Combine(DataRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(StandaloneHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Production",
            WebRootPath = webRoot,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.UseSetting("suppressHostingStartup", "true");

        const string tenant = "Grove:Grove_AutoSetup:Tenants:0:";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ServerHostDefaults.DataRootKey] = DataRoot,
            ["Grove:Grove_AutoSetup:AutoSetupPath"] = string.Empty,
            [tenant + "ShellName"] = ShellSettings.DefaultShellName,
            [tenant + "SiteName"] = "Grove standalone",
            [tenant + "SiteTimeZone"] = "Etc/UTC",
            [tenant + "AdminUsername"] = AdminUserName,
            [tenant + "AdminEmail"] = "admin@example.com",
            [tenant + "AdminPassword"] = "Standalone1!",
            [tenant + "DatabaseProvider"] = "Sqlite",
            [tenant + "DatabaseConnectionString"] = string.Empty,
            [tenant + "DatabaseTablePrefix"] = string.Empty,
            [tenant + "RecipeName"] = RecipeName,
        });

        builder.Services
            .AddPlatformCms()
            .AddSetupFeatures("Grove.Infrastructure.Setup.Auto");
        builder.Services.AddHostDefaults();

        _app = builder.Build();
        _app.UseHostStaticFiles();
        _app.UsePlatform();
        await _app.StartAsync(TestContext.Current.CancellationToken);

        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        Client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromMinutes(10) };

        await WaitUntilProvisionedAsync();
    }

    /// <summary>Runs <paramref name="execute"/> in a scope of the Default tenant; the scope's unit of work commits when it ends.</summary>
    public async Task InDefaultTenantAsync(Func<IServiceProvider, Task> execute)
    {
        var scope = await ShellHost.GetScopeAsync(ShellSettings.DefaultShellName);
        await scope.UsingAsync(shellScope => execute(shellScope.ServiceProvider));
    }

    // The first request runs the setup recipe (every feature's migrations, a few minutes on a
    // slow disk); provisioned means the login page renders its form.
    private async Task WaitUntilProvisionedAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(10);
        string? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await Client.GetAsync("login", TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                if (response.IsSuccessStatusCode && body.Contains("UserName", StringComparison.Ordinal))
                {
                    return;
                }

                last = $"HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !TestContext.Current.CancellationToken.IsCancellationRequested))
            {
                last = ex.Message;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"The standalone host did not provision within ten minutes (last: {last}).");
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        try
        {
            Directory.Delete(DataRoot, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may hold a file a moment longer; the temp folder is the OS's to clean.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
