using Microsoft.AspNetCore.Mvc;

namespace Grove.Demo.Controllers;

public sealed class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
