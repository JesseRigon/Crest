namespace Crest.Media;

public interface IMediaNameNormalizerService
{
    string NormalizeFolderName(string folderName);
    string NormalizeFileName(string fileName);
}
