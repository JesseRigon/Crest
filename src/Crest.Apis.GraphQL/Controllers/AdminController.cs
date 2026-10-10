using Microsoft.AspNetCore.Mvc;
using Crest.Admin;

namespace Crest.Apis.GraphQL.Controllers;

public sealed class AdminController : Controller
{
    [HttpGet]
    [Admin("GraphQL", "GraphQL")]
    public IActionResult Index()
    {
        return View();
    }
}
