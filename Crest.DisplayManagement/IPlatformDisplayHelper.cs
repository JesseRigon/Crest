namespace Crest.DisplayManagement.Razor;

public interface IPlatformDisplayHelper : IPlatformHelper
{
    IDisplayHelper DisplayHelper { get; }
}
