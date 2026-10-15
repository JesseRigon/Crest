using Grove.Tests.Functional.Tests.Cms;
using Xunit;

namespace Grove.Tests.Functional;

[CollectionDefinition(Name)]
public sealed class CmsTestCollection : ICollectionFixture<SaasFixture>
{
    public const string Name = "CMS";
}
