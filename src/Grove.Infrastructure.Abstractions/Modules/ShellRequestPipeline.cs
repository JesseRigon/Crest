using Microsoft.AspNetCore.Http;
using Grove.Environment.Shell.Builders;

namespace Grove.Modules;

public class ShellRequestPipeline : IShellPipeline
{
    public RequestDelegate Next { get; set; }
    public Task Invoke(object context) => Next(context as HttpContext);
}
