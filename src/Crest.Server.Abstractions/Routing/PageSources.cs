namespace Crest.Routing;

/// <summary>
/// Declared page registry entries from module code that a component attribute cannot carry:
/// legacy MVC pages (until they are ported), menu groups, a kind registry's pages. Read once when
/// the tenant shell builds, so a source answers from code and shell-lifetime state (options,
/// the shell's settings, a kind catalog), never from tenant data that changes at runtime.
/// </summary>
public interface IPageSource
{
    IEnumerable<PageEntry> GetPages();
}

/// <summary>
/// Page registry entries generated from tenant data (New › content type, content groups, custom
/// settings types, designed pages): read per request from a source that caches itself and is
/// invalidated when its data changes, then added after the declared entries.
/// </summary>
public interface IDynamicPageSource
{
    Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default);
}

/// <summary>
/// The page registry (docs/admin-menu.md › The page registry), owned by Crest.Server: the
/// declared layer (component declarations and <see cref="IPageSource"/>s, built when the shell
/// builds and following feature enablement and the active admin theme) plus the dynamic entries.
/// </summary>
public interface IPageRegistry
{
    /// <summary>The declared entries only.</summary>
    Task<PageSet> GetDeclaredAsync();

    /// <summary>Every entry: declared, then dynamic.</summary>
    Task<PageSet> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>A built set of page registry entries.</summary>
public sealed class PageSet
{
    private readonly Dictionary<(ShellId Shell, string Id), PageEntry> _byId = [];
    private readonly List<(PageEntry Entry, string Template, int Specificity)> _gates = [];

    public PageSet(IEnumerable<PageEntry> entries)
    {
        var list = new List<PageEntry>();
        foreach (var entry in entries)
        {
            // The first declaration of an id wins; the registry reports duplicates when it builds.
            if (_byId.TryAdd((entry.Shell, entry.Id), entry))
            {
                list.Add(entry);
                if (!entry.DeclaredBySource)
                {
                    foreach (var route in entry.Routes)
                    {
                        _gates.Add((entry, route, RouteTemplates.Specificity(route)));
                    }
                }
            }
        }

        Entries = list;
    }

    public IReadOnlyList<PageEntry> Entries { get; }

    public IEnumerable<PageEntry> InShell(ShellId shell) => Entries.Where(entry => entry.Shell == shell);

    public PageEntry? Find(ShellId shell, string id) => _byId.GetValueOrDefault((shell, id));

    /// <summary>
    /// The entry whose route admits <paramref name="path"/> in <paramref name="shell"/>: the most
    /// specific matching template (most literal segments), the first declared among equals.
    /// </summary>
    public PageEntry? MatchRoute(ShellId shell, string? path)
    {
        PageEntry? best = null;
        var bestSpecificity = -1;
        foreach (var (entry, template, specificity) in _gates)
        {
            if (entry.Shell == shell && specificity > bestSpecificity && RouteTemplates.Matches(template, path))
            {
                best = entry;
                bestSpecificity = specificity;
            }
        }

        return best;
    }

    /// <summary>Every gated route of a shell with the entry that declares it.</summary>
    public IEnumerable<(PageEntry Entry, string Template)> Routes(ShellId shell) =>
        _gates.Where(gate => gate.Entry.Shell == shell).Select(gate => (gate.Entry, gate.Template));

    public PageSet With(IEnumerable<PageEntry> more) => new([.. Entries, .. more]);
}
