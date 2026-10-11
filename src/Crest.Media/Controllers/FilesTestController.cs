using Crest.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Media.Controllers;

/// <summary>
/// A bare page that drives <c>api/files</c> from a browser (drives, a folder, an upload, the
/// children, a download), only to confirm the file system answers end to end. Not the drive
/// browser (docs/media.md § 8).
/// </summary>
[Authorize]
[Admin("Files/Test", "Files.Test")]
public sealed class FilesTestController : Controller
{
    public IActionResult Index() => View();
}
