using Microsoft.AspNetCore.Mvc;

namespace Crest.Demo.Components;

public class FakeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string value)
    {
        return View("Default", value);
    }
}
