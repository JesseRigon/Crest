namespace Crest.Media.Indexing;

public class TextMediaFileTextProvider : IMediaFileTextProvider
{
    public async Task<string> GetTextAsync(string path, Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);

        return await reader.ReadToEndAsync();
    }
}
