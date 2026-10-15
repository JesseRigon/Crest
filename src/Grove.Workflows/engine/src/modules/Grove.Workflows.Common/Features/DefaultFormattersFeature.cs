using System.ComponentModel;
using Grove.Workflows.Common.Serialization;
using Grove.Workflows.Common.Services;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Common.Features;

public class DefaultFormattersFeature(IModule module) : FeatureBase(module)
{
    public override void Configure()
    {
        TypeDescriptor.AddAttributes(typeof(Type), new TypeConverterAttribute(typeof(TypeTypeConverter)));
        Module.Services.AddSingleton<IFormatter, JsonFormatter>();
    }
}