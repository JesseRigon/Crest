using Microsoft.AspNetCore.Http;
using Crest.Environment.Shell.Builders;

namespace Crest.Modules;

public class ShellRequestPipeline : IShellPipeline
{
    public RequestDelegate Next { get; set; }
    public Task Invoke(object context) => Next(context as HttpContext);
}
