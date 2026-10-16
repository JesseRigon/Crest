namespace Grove.Contents.Management;

public abstract class ContentFieldSetOptionBase
{
    public ContentFieldSetOptionBase(Type contentFieldSetType)
    {
        ArgumentNullException.ThrowIfNull(contentFieldSetType);

        Type = contentFieldSetType;
    }

    public Type Type { get; }
}
