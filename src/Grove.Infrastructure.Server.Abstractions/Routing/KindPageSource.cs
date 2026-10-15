using System.Globalization;
using Grove.Data.Kinds;

namespace Grove.Routing;

/// <summary>
/// The pages of a kind registry (docs/architecture.md › Kind-registry plumbing): a menu section
/// whose own link is the registry's "All" page, an "All" child that reaches it (the primary nav
/// turns a parent with children into an expander, never a link), and one child per registered
/// kind in catalog order, each gated at its concrete route when the registry's generic page
/// serves it. A kind whose descriptor names its own route is its owner's page to gate; its menu
/// item still links there. The generic page component declares
/// <c>[PageEntry(DeclaredBySource = true)]</c>, so its template admits nothing on its own.
/// </summary>
public abstract class KindPageSource<TKind>(KindCatalog<TKind> catalog) : IPageSource
    where TKind : IKindDescriptor
{
    /// <summary>What the section looks like.</summary>
    /// <param name="Caption">Invariant section caption.</param>
    /// <param name="Position">The section's position in the menu.</param>
    /// <param name="Id">The section's entry id; its children's ids derive from it by default.</param>
    /// <param name="Icon">The section icon.</param>
    /// <param name="AllRoute">The registry's "All" page (shell-relative).</param>
    /// <param name="AllCaption">Invariant caption of the "All" child.</param>
    /// <param name="Permission">The permission every page of the registry asks for.</param>
    protected sealed record Section(
        string Caption,
        string Position,
        string Id,
        string Icon,
        string AllRoute,
        string AllCaption,
        string Permission);

    protected abstract Section Describe();

    /// <summary>The generic page route of a kind ("/Assets/{key}").</summary>
    protected abstract string GenericRoute(string key);

    /// <summary>The kind's invariant menu caption; its display name by default.</summary>
    protected virtual string CaptionOf(TKind kind) => kind.DisplayName;

    /// <summary>The "All" child's entry id; "{section id}-all" by default.</summary>
    protected virtual string AllEntryIdOf(Section section) => $"{section.Id}-all";

    /// <summary>The kind's entry id; "{section id}-{key}" by default.</summary>
    protected virtual string EntryIdOf(Section section, TKind kind) => $"{section.Id}-{kind.Key}";

    /// <summary>Whether the registry's generic page serves this kind; by default, a kind with no route of its own.</summary>
    protected virtual bool Serves(TKind kind) => string.IsNullOrWhiteSpace(kind.Route);

    public IEnumerable<PageEntry> GetPages()
    {
        var section = Describe();
        var declaredBy = GetType().FullName;
        string[] permissions = [section.Permission];

        yield return new PageEntry
        {
            Id = section.Id,
            Routes = [section.AllRoute],
            Permissions = permissions,
            DeclaredBy = declaredBy,
            Menu = new PageMenu(section.Caption, Position: section.Position, Icon: section.Icon) { Url = section.AllRoute },
        };

        yield return new PageEntry
        {
            Id = AllEntryIdOf(section),
            Permissions = permissions,
            DeclaredBy = declaredBy,
            Menu = new PageMenu(section.AllCaption, section.Id, "0", section.Icon) { Url = section.AllRoute },
        };

        var position = 0;
        foreach (var kind in catalog.Kinds)
        {
            position++;
            var route = KindCatalog<TKind>.RouteOf(kind, GenericRoute);
            yield return new PageEntry
            {
                Id = EntryIdOf(section, kind),
                Routes = Serves(kind) ? [route] : [],
                Permissions = permissions,
                DeclaredBy = declaredBy,
                Menu = new PageMenu(CaptionOf(kind), section.Id, position.ToString(CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(kind.Icon) ? null : kind.Icon)
                {
                    Url = route,
                },
            };
        }
    }
}
