using Microsoft.AspNetCore.Http;

namespace Grove.Environment.Shell;

public interface IRunningShellTable
{
    void Add(ShellSettings settings);
    void Remove(ShellSettings settings);
    ShellSettings Match(HostString host, PathString path, bool fallbackToDefault = true);
}
