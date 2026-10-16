using System.Collections.Frozen;

namespace Grove.Contents.Management;

/// <summary>
/// Provides a way to register code based field sets and fields.
/// </summary>
public class ContentOptions
{
    private readonly List<ContentFieldSetOption> _contentFieldSets = [];
    private readonly List<ContentFieldOption> _contentFields = [];

    private IReadOnlyDictionary<string, ContentFieldSetOption> _contentFieldSetOptionsLookup;
    public IReadOnlyDictionary<string, ContentFieldSetOption> ContentFieldSetOptionsLookup
        => _contentFieldSetOptionsLookup ??= ContentFieldSetOptions.ToFrozenDictionary(k => ContentFieldSetName.Of(k.Type));

    private IReadOnlyDictionary<string, ContentFieldOption> _contentFieldOptionsLookup;
    public IReadOnlyDictionary<string, ContentFieldOption> ContentFieldOptionsLookup
        => _contentFieldOptionsLookup ??= ContentFieldOptions.ToFrozenDictionary(k => k.Type.Name);

    public IReadOnlyList<ContentFieldSetOption> ContentFieldSetOptions => _contentFieldSets;
    public IReadOnlyList<ContentFieldOption> ContentFieldOptions => _contentFields;

    internal void AddFieldSetHandler(Type contentFieldSetType, Type handlerType)
    {
        var option = GetOrAddContentFieldSet(contentFieldSetType);
        option.AddHandler(handlerType);
    }

    internal void RemoveFieldSetHandler(Type contentFieldSetType, Type handlerType)
    {
        var option = GetOrAddContentFieldSet(contentFieldSetType);
        option.RemoveHandler(handlerType);
    }

    internal ContentFieldSetOption GetOrAddContentFieldSet(Type contentFieldSetType)
    {
        if (!contentFieldSetType.IsSubclassOf(typeof(ContentFieldSet)))
        {
            throw new ArgumentException("The type must inherit from " + nameof(ContentFieldSet));
        }

        var option = _contentFieldSets.FirstOrDefault(x => x.Type == contentFieldSetType);
        if (option == null)
        {
            option = new ContentFieldSetOption(contentFieldSetType);
            _contentFieldSets.Add(option);
        }

        return option;
    }

    internal void AddFieldHandler(Type contentFieldType, Type handlerType)
    {
        var option = GetOrAddContentField(contentFieldType);
        option.AddHandler(handlerType);
    }

    internal void RemoveFieldHandler(Type contentFieldType, Type handlerType)
    {
        var option = GetOrAddContentField(contentFieldType);
        option.RemoveHandler(handlerType);
    }

    internal ContentFieldOption GetOrAddContentField(Type contentFieldType)
    {
        if (!contentFieldType.IsSubclassOf(typeof(ContentField)))
        {
            throw new ArgumentException("The type must inherit from " + nameof(ContentField));
        }

        var option = _contentFields.FirstOrDefault(x => x.Type == contentFieldType);
        if (option == null)
        {
            option = new ContentFieldOption(contentFieldType);
            _contentFields.Add(option);
        }

        return option;
    }
}
