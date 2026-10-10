using Microsoft.AspNetCore.Mvc;

namespace Crest.Demo.Controllers;

public sealed class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
