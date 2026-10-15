namespace Grove.Media.Services;

public interface IMediaProfileService
{
    Task<IDictionary<string, string>> GetMediaProfileCommands(string name);
}
