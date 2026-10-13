using Crest.Access;
using Crest.Parties.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crest.Parties.Tests;

// Parties' rows are readable exactly where their content item is (docs/access.md, Domains
// onto the extension system): the parties scope is a semi-join on the caller's content scope.
public class PartyScopeProviderTests
{
    private sealed class ContentScope(ScopeRule rule) : IScopeProvider
    {
        public string Target => "ContentItemIndex";

        public int Version => 1;

        public Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default) => Task.FromResult(rule);
    }

    private static readonly CallerContext Caller = new() { Tenant = "t", Side = CallerSide.Admin, UserId = "u-1" };

    private static Task<ScopeRule> BuildAsync(params ScopeRule[] content)
    {
        var services = new ServiceCollection();
        foreach (var rule in content)
        {
            services.AddSingleton<IScopeProvider>(new ContentScope(rule));
        }

        services.AddSingleton<IScopeProvider>(provider => new PartyScopeProvider("LocationIndex", provider));
        var provider = services.BuildServiceProvider();
        return provider.GetServices<IScopeProvider>().Single(scope => scope.Target == "LocationIndex").BuildAsync(Caller, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Rows_follow_the_content_scope()
    {
        Assert.Equal(ScopeKind.All, (await BuildAsync(ScopeRule.All)).Kind);
        Assert.Equal(ScopeKind.None, (await BuildAsync(ScopeRule.All, ScopeRule.None)).Kind);

        var owned = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.Equal("Owner", "u-1")));
        var rule = await BuildAsync(owned);
        Assert.Equal(ScopeKind.Filter, rule.Kind);
        Assert.Equal(ScopeOperator.InSelect, rule.Filter!.Condition!.Operator);
        Assert.Equal("ContentItemIndex", rule.Filter.Condition.Select!.Table);
    }

    [Fact]
    public async Task No_content_scope_reads_nothing()
    {
        Assert.Equal(ScopeKind.None, (await BuildAsync()).Kind);
    }
}
