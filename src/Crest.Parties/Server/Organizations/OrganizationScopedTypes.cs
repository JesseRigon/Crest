namespace Crest.Parties.Organizations;

/// <summary>Which content types carry <see cref="CrestOrganizationPart"/> and are scoped by
/// organization; a deployment decision declared once (a singleton), like assignment scoping.</summary>
public sealed class OrganizationScopedTypes
{
    private readonly HashSet<string> _types = new(StringComparer.OrdinalIgnoreCase);

    public OrganizationScopedTypes Add(string contentType)
    {
        _types.Add(contentType);
        return this;
    }

    public bool IsScoped(string contentType) => _types.Contains(contentType);

    public IEnumerable<string> Types => _types;
}
