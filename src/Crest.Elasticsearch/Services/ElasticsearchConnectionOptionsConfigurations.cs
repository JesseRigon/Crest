using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell.Configuration;
using Crest.Elasticsearch.Models;

namespace Crest.Elasticsearch.Services;

public sealed class ElasticsearchConnectionOptionsConfigurations : IConfigureOptions<ElasticsearchConnectionOptions>
{
    public const string ConfigSectionName = "Crest_Elasticsearch";

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
