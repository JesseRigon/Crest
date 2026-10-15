using Grove.Environment.Shell;
using Grove.Environment.Shell.Builders;
using Grove.Environment.Shell.Descriptor.Models;
using Grove.Shells.Database.Configuration;

namespace Grove.Shells.Database.Extensions;

public static class DatabaseShellContextFactoryExtensions
{
    internal static Task<ShellContext> GetDatabaseContextAsync(
        this IShellContextFactory shellContextFactory, DatabaseShellsStorageOptions options)
    {
        if (options.DatabaseProvider is null)
        {
            throw new InvalidOperationException("The 'Grove.Shells.Database' configuration section should define a 'DatabaseProvider'");
        }

        var settings = new ShellSettings()
            .AsDefaultShell()
            .AsDisposable()
            .AsRunning();

        settings["DatabaseProvider"] = options.DatabaseProvider;
        settings["ConnectionString"] = options.ConnectionString;
        settings["TablePrefix"] = options.TablePrefix;
        settings["Schema"] = options.Schema;

        return shellContextFactory.CreateDescribedContextAsync(settings, new ShellDescriptor());
    }
}
