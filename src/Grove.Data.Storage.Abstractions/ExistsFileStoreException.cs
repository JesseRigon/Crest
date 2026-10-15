namespace Grove.Data.Storage;

public class ExistsFileStoreException : FileStoreException
{
    public ExistsFileStoreException(string message) : base(message)
    {
    }

    public ExistsFileStoreException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
