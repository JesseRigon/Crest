using Grove.Access;

namespace Grove.Tests.Access;

// Per-part access as scope (docs/permissions.md, ruling 2026-10-10): a part with no provider
// follows the item; a provider's rule shows, hides or filters the part per item.
public class ContentFieldSetScopeTests
{
    private static ScopeSet Set(ScopeRule rule) =>
        new("sig", new Dictionary<string, ScopeRule> { [ContentFieldSetScope.Target("Product", "CostPart")] = rule });

    private static object Row(string column) => column switch
    {
        "Owner" => "u-1",
        "ContentType" => "Product",
        _ => null,
    };

    [Fact]
    public void A_part_without_a_provider_follows_the_item()
        => Assert.True(ContentFieldSetScope.Admits(Set(ScopeRule.None), "Product", "Title", Row));

    [Fact]
    public void A_provider_shows_hides_or_filters_the_part()
    {
        Assert.True(ContentFieldSetScope.Admits(Set(ScopeRule.All), "Product", "CostPart", Row));
        Assert.False(ContentFieldSetScope.Admits(Set(ScopeRule.None), "Product", "CostPart", Row));
        Assert.True(ContentFieldSetScope.Admits(Set(ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u-1")))), "Product", "CostPart", Row));
        Assert.False(ContentFieldSetScope.Admits(Set(ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u-2")))), "Product", "CostPart", Row));
    }

    [Fact]
    public void A_semi_join_rule_fails_closed()
    {
        var rule = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.InSelect("ContentItemId", "ShareAccessIndex", "ObjectId", ScopeFilter.Of(ScopeCondition.Equal("UserId", "u-1")))));
        Assert.False(ContentFieldSetScope.Admits(Set(rule), "Product", "CostPart", Row));
    }
}
