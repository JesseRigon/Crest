namespace Grove.Contents.Management.Display.ContentDisplay;

public class ContentFieldSetDisplayOption : ContentFieldSetOptionBase
{
    private readonly List<ContentFieldSetDisplayDriverOption> _fieldSetDisplayDrivers = [];

    public ContentFieldSetDisplayOption(Type contentFieldSetType) : base(contentFieldSetType)
    {
    }

    private IReadOnlyList<ContentFieldSetDisplayDriverOption> _displayDrivers;
    private IReadOnlyList<ContentFieldSetDisplayDriverOption> _editorDrivers;

    public IReadOnlyList<ContentFieldSetDisplayDriverOption> DisplayDrivers
        => _displayDrivers ??= _fieldSetDisplayDrivers.Where(d => d.DisplayMode != null).ToList();

    public IReadOnlyList<ContentFieldSetDisplayDriverOption> EditorDrivers
        => _editorDrivers ??= _fieldSetDisplayDrivers.Where(d => d.Editor != null).ToList();

    internal void ForDisplayMode(Type displayDriverType, Func<string, bool> predicate)
    {
        var option = GetOrAddContentFieldSetDisplayDriverOption(displayDriverType);

        option.SetDisplayMode(predicate);
    }

    internal void ForEditor(Type displayDriverType, Func<string, bool> predicate)
    {
        var option = GetOrAddContentFieldSetDisplayDriverOption(displayDriverType);

        option.SetEditor(predicate);
    }

    internal void RemoveDisplayDriver(Type displayDriverType)
    {
        var displayDriverOption = _fieldSetDisplayDrivers.FirstOrDefault(d => d.DisplayDriverType == displayDriverType);
        if (displayDriverOption != null)
        {
            _fieldSetDisplayDrivers.Remove(displayDriverOption);
        }
    }

    private ContentFieldSetDisplayDriverOption GetOrAddContentFieldSetDisplayDriverOption(Type displayDriverType)
    {
        var option = _fieldSetDisplayDrivers.FirstOrDefault(o => o.DisplayDriverType == displayDriverType);

        if (option == null)
        {
            option = new ContentFieldSetDisplayDriverOption(displayDriverType);
            _fieldSetDisplayDrivers.Add(option);
        }

        return option;
    }
}
