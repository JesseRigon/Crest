using Microsoft.AspNetCore.Mvc;

namespace Crest.Feeds.Models;

public class ContextualizeContext
{
    public IServiceProvider ServiceProvider { get; set; }
    public IUrlHelper Url { get; set; }
}
