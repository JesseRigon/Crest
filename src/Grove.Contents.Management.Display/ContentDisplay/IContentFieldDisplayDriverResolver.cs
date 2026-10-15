namespace Grove.Contents.Management.Display.ContentDisplay;

public interface IContentFieldDisplayDriverResolver
{
    IList<IContentFieldDisplayDriver> GetDisplayModeDrivers(string fieldName, string displayMode);
    IList<IContentFieldDisplayDriver> GetEditorDrivers(string fieldName, string editor);
}
