using Microsoft.AspNetCore.Mvc;
using Crest.Diagnostics.ViewModels;

namespace Crest.Diagnostics.Controllers;

public sealed class DiagnosticsController : Controller
{
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? status)
    {
        // Most commonly used error messages.
        ViewData["StatusCode"] = status;

        return View(new HttpErrorShapeViewModel(status));
    }
}
