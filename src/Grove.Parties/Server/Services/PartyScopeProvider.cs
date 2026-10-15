using Grove.Access;
using Grove.Contents.Management.Records;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Parties.Services;

/// <summary>
/// The rows of a parties index table (<c>LocationIndex</c>, <c>PartyPositionIndex</c>) a caller
/// may read (docs/access.md, Domains onto the extension system): exactly the rows whose content
/// item the caller may see, a semi-join on <see cref="ContentItemIndex"/> under the caller's
/// content scope, so the content, assignment and organization scopes registered for content
/// items (organization-owned parties included) bound the parties reads with nothing decided
/// here twice. The content scope is built from the content item providers themselves, read
/// when the rule is built (not at construction, where they would include this one).
/// </summary>
public sealed class PartyScopeProvider(string target, IServiceProvider services) : IScopeProvider
{
    public string Target => target;

    public int Version => 1;

    public async Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contentProviders = services.GetServices<IScopeProvider>()
            .Concat(services.GetServices<IScopeProviderSource>().SelectMany(source => source.GetProviders()))
            .Where(provider => string.Equals(provider.Target, nameof(ContentItemIndex), StringComparison.OrdinalIgnoreCase));

        ScopeRule? content = null;
        foreach (var provider in contentProviders)
        {
            var built = await provider.BuildAsync(caller, cancellationToken);
            content = content is null ? built : content.And(built);
        }

        // No content scope means content items cannot be read at all: neither can their rows here.
        return content?.Kind switch
        {
            ScopeKind.All => ScopeRule.All,
            ScopeKind.Filter => ScopeRule.Where(ScopeFilter.Of(ScopeCondition.InSelect(
                nameof(ContentItemIndex.ContentItemId), nameof(ContentItemIndex), nameof(ContentItemIndex.ContentItemId), content.Filter!))),
            _ => ScopeRule.None,
        };
    }
}
