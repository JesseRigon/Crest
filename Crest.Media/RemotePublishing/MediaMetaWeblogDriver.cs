using Crest.MetaWeblog;

namespace Crest.Media.RemotePublishing;

public sealed class MediaMetaWeblogDriver : MetaWeblogDriver
{
    public override void SetCapabilities(Action<string, string> setCapability)
    {
        setCapability("supportsFileUpload", "Yes");
    }
}
