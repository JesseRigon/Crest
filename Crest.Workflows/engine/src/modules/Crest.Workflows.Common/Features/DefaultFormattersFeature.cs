using System.ComponentModel;
using Crest.Workflows.Common.Serialization;
using Crest.Workflows.Common.Services;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Features;

public class DefaultFormattersFeature(IModule module) : FeatureBase(module)
{
    public override void Configure()
    {
        TypeDescriptor.AddAttributes(typeof(Type), new TypeConverterAttribute(typeof(TypeTypeConverter)));
        Module.Services.AddSingleton<IFormatter, JsonFormatter>();
    }
}