using Crest.Tests.Functional.Tests.Cms;
using Xunit;

namespace Crest.Tests.Functional;

[CollectionDefinition(Name)]
public sealed class CmsTestCollection : ICollectionFixture<SaasFixture>
{
    public const string Name = "CMS";
}
