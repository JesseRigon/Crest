using Microsoft.AspNetCore.Http;

namespace Crest.Environment.Shell;

public interface IRunningShellTable
{
    void Add(ShellSettings settings);
    void Remove(ShellSettings settings);
    ShellSettings Match(HostString host, PathString path, bool fallbackToDefault = true);
}
