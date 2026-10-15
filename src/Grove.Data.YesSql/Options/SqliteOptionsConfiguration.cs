using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;

namespace Grove.Data;

public sealed class SqliteOptionsConfiguration : IConfigureOptions<SqliteOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public SqliteOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(SqliteOptions options)
    {
        var section = _shellConfiguration.GetSection("Grove_Data_Sqlite");

        section.Bind(options);
    }
}
