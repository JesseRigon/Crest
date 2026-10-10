using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell.Configuration;

namespace Crest.Data;

public sealed class SqliteOptionsConfiguration : IConfigureOptions<SqliteOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public SqliteOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(SqliteOptions options)
    {
        var section = _shellConfiguration.GetSection("Crest_Data_Sqlite");

        section.Bind(options);
    }
}
