using System.Collections.Frozen;

namespace Grove.Contents.Management.Display.ContentDisplay;

public class ContentDisplayOptions
{
    private readonly List<ContentFieldSetDisplayOption> _contentFieldSets = [];
    private readonly List<ContentFieldDisplayOption> _contentFields = [];

    private FrozenDictionary<string, ContentFieldSetDisplayOption> _contentFieldSetOptions;
    private FrozenDictionary<string, ContentFieldDisplayOption> _contentFieldOptions;

    public IReadOnlyDictionary<string, ContentFieldSetDisplayOption> ContentFieldSetOptions
        => _contentFieldSetOptions ??= _contentFieldSets.ToFrozenDictionary(k => ContentFieldSetName.Of(k.Type));

    public IReadOnlyDictionary<string, ContentFieldDisplayOption> ContentFieldOptions
        => _contentFieldOptions ??= _contentFields.ToFrozenDictionary(k => k.Type.Name);

    internal void ForContentFieldSetDisplayMode(Type contentFieldSetType, Type displayDriverType, Func<string, bool> predicate)
    {
        if (!typeof(IContentFieldSetDisplayDriver).IsAssignableFrom(displayDriverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldSetDisplayDriver));
        }

        var option = GetOrAddContentFieldSetDisplayOption(contentFieldSetType);
        option.ForDisplayMode(displayDriverType, predicate);
    }

    internal void ForContentFieldSetEditor(Type contentFieldSetType, Type editorDriverType, Func<string, bool> predicate)
    {
        if (!typeof(IContentFieldSetDisplayDriver).IsAssignableFrom(editorDriverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldSetDisplayDriver));
        }

        var option = GetOrAddContentFieldSetDisplayOption(contentFieldSetType);
        option.ForEditor(editorDriverType, predicate);
    }

    internal void RemoveContentFieldSetDisplayDriver(Type contentFieldSetType, Type driverType)
    {
        if (!typeof(IContentFieldSetDisplayDriver).IsAssignableFrom(driverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldSetDisplayDriver));
        }

        var option = GetOrAddContentFieldSetDisplayOption(contentFieldSetType);
        option.RemoveDisplayDriver(driverType);
    }

    private ContentFieldSetDisplayOption GetOrAddContentFieldSetDisplayOption(Type contentFieldSetType)
    {
        if (!contentFieldSetType.IsSubclassOf(typeof(ContentFieldSet)))
        {
            throw new ArgumentException("The type must inherit from " + nameof(ContentFieldSet));
        }

        var option = _contentFieldSets.FirstOrDefault(x => x.Type == contentFieldSetType);
        if (option == null)
        {
            option = new ContentFieldSetDisplayOption(contentFieldSetType);
            _contentFieldSets.Add(option);
        }

        return option;
    }

    internal void ForContentFieldDisplayMode(Type contentFieldType, Type displayModeDriverType, Func<string, bool> predicate)
    {
        if (!typeof(IContentFieldDisplayDriver).IsAssignableFrom(displayModeDriverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldDisplayDriver));
        }

        var option = GetOrAddContentFieldDisplayOption(contentFieldType);
        option.ForDisplayMode(displayModeDriverType, predicate);
    }

    internal void ForContentFieldEditor(Type contentFieldType, Type editorDriverType, Func<string, bool> predicate)
    {
        if (!typeof(IContentFieldDisplayDriver).IsAssignableFrom(editorDriverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldDisplayDriver));
        }

        var option = GetOrAddContentFieldDisplayOption(contentFieldType);
        option.ForEditor(editorDriverType, predicate);
    }

    internal void RemoveContentFieldDisplayDriver(Type contentFieldSetType, Type driverType)
    {
        if (!typeof(IContentFieldDisplayDriver).IsAssignableFrom(driverType))
        {
            throw new ArgumentException("The type must implement " + nameof(IContentFieldDisplayDriver));
        }

        var option = GetOrAddContentFieldDisplayOption(contentFieldSetType);
        option.RemoveDisplayDriver(driverType);
    }

    private ContentFieldDisplayOption GetOrAddContentFieldDisplayOption(Type contentFieldType)
    {
        if (!contentFieldType.IsSubclassOf(typeof(ContentField)))
        {
            throw new ArgumentException("The type must inherit from " + nameof(ContentField));
        }

        var option = _contentFields.FirstOrDefault(x => x.Type == contentFieldType);
        if (option == null)
        {
            option = new ContentFieldDisplayOption(contentFieldType);
            _contentFields.Add(option);
        }

        return option;
    }
}
