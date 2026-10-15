using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Admin.Controllers;

[Authorize]
public sealed class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
