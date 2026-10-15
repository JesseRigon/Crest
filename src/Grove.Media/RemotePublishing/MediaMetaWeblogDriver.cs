using Grove.MetaWeblog;

namespace Grove.Media.RemotePublishing;

public sealed class MediaMetaWeblogDriver : MetaWeblogDriver
{
    public override void SetCapabilities(Action<string, string> setCapability)
    {
        setCapability("supportsFileUpload", "Yes");
    }
}
