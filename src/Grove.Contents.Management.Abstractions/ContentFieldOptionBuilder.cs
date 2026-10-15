using Microsoft.Extensions.DependencyInjection;

namespace Grove.Contents.Management;

public class ContentFieldOptionBuilder
{
    public ContentFieldOptionBuilder(IServiceCollection services, Type contentFieldType)
    {
        Services = services;
        ContentFieldType = contentFieldType;
    }

    public IServiceCollection Services { get; }
    public Type ContentFieldType { get; }
}
