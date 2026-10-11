using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.Users.Models;
using Crest.Users.Workflows;
using Crest.Workflows;

namespace Crest.Users.Controllers;

public abstract class AccountBaseController : Controller
{
    protected async Task<IActionResult> LoggedInActionResultAsync(IUser user, string returnUrl = null, ExternalLoginInfo info = null)
    {
        var triggers = HttpContext.RequestServices.GetService<IWorkflowTriggerPublisher>();
        if (triggers is not null && user is User platformUser)
        {
            await triggers.PublishAsync(UserWorkflowTriggers.LoggedIn, platformUser.UserId, UserWorkflowTriggers.Payload(platformUser, info?.LoginProvider ?? string.Empty));
        }

        return RedirectToLocal(returnUrl);
    }

    protected IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl.ToUriComponents());
        }

        return Redirect("~/");
    }

    protected void CopyTempDataErrorsToModelState()
    {
        foreach (var errorMessage in TempData.Where(x => x.Key.StartsWith("error")).Select(x => x.Value.ToString()))
        {
            ModelState.AddModelError(string.Empty, errorMessage);
        }
    }
}
