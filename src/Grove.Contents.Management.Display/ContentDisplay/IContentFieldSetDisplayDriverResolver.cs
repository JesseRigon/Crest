namespace Grove.Contents.Management.Display.ContentDisplay;

public interface IContentFieldSetDisplayDriverResolver
{
    IList<IContentFieldSetDisplayDriver> GetDisplayModeDrivers(string fieldSetName, string displayMode);
    IList<IContentFieldSetDisplayDriver> GetEditorDrivers(string fieldSetName, string editor);
}
