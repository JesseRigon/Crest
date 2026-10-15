namespace Grove.Contents.Management;

public abstract class ContentPartOptionBase
{
    public ContentPartOptionBase(Type contentPartType)
    {
        ArgumentNullException.ThrowIfNull(contentPartType);

        Type = contentPartType;
    }

    public Type Type { get; }
}
