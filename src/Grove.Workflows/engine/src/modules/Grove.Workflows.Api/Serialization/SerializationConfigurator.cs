using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Api.Serialization;

internal class SerializationConfigurator(IServiceProvider serviceProvider) : SerializationOptionsConfiguratorBase
{
    public override void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(ActivatorUtilities.GetServiceOrCreateInstance<ArgumentJsonConverterFactory>(serviceProvider));
    }
}