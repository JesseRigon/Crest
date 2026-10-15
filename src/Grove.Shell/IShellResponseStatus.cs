namespace Grove.Shell;

/// <summary>
/// Lets a shell page set the response status while it is prerendered (a site path with no page
/// answers 404 with the layout around it). Only the server registers it; in the browser and in a
/// circuit there is no response left to set, and pages resolve it as optional.
/// </summary>
public interface IShellResponseStatus
{
    void Set(int statusCode);
}
