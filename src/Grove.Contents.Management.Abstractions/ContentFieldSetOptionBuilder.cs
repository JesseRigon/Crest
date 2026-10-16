using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Management;

public class ContentFieldSetOptionBuilder
{
    public ContentFieldSetOptionBuilder(IServiceCollection services, Type contentFieldSetType)
    {
        Services = services;
        ContentFieldSetType = contentFieldSetType;
    }

    public IServiceCollection Services { get; }
    public Type ContentFieldSetType { get; }
}
