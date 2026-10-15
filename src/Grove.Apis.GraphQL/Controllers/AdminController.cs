using Microsoft.AspNetCore.Mvc;
using Grove.Admin;

namespace Grove.Apis.GraphQL.Controllers;

public sealed class AdminController : Controller
{
    [HttpGet]
    [Admin("GraphQL", "GraphQL")]
    public IActionResult Index()
    {
        return View();
    }
}
