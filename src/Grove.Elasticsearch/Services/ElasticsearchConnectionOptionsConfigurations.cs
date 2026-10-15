using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Elasticsearch.Models;

namespace Grove.Elasticsearch.Services;

public sealed class ElasticsearchConnectionOptionsConfigurations : IConfigureOptions<ElasticsearchConnectionOptions>
{
    public const string ConfigSectionName = "Grove_Elasticsearch";

    private readonly IShellConfiguration _shellConfiguration;

    public ElasticsearchConnectionOptionsConfigurations(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(ElasticsearchConnectionOptions options)
    {
        _shellConfiguration.GetSection(ConfigSectionName).Bind(options);

        if (options.Ports == null || options.Ports.Length == 0)
        {
            options.Ports = [9200];
        }

        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            options.SetFileConfigurationExists(true);
        }
    }
}
