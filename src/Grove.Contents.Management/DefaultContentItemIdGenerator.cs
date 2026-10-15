using Grove.Entities;

namespace Grove.Contents.Management;

public class DefaultContentItemIdGenerator : IContentItemIdGenerator
{
    private readonly IIdGenerator _generator;

    public DefaultContentItemIdGenerator(IIdGenerator generator)
    {
        _generator = generator;
    }

    public string GenerateUniqueId(ContentItem contentItem)
    {
        return _generator.GenerateUniqueId();
    }
}
