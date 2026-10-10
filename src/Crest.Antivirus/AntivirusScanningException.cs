using Crest.FileStorage;

namespace Crest.Antivirus;

public class AntivirusScanningException : FileStoreException
{
    public AntivirusScanningException(string message)
        : base(message)
    {
    }

    public AntivirusScanningException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
