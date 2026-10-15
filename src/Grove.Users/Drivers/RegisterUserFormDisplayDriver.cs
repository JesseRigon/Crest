using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Mvc.ModelBinding;
using Grove.Users.Models;
using Grove.Users.ViewModels;

namespace Grove.Users.Drivers;

public sealed class RegisterUserFormDisplayDriver : DisplayDriver<RegisterUserForm>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IdentityOptions _identityOptions;

    internal readonly IStringLocalizer S;

    public RegisterUserFormDisplayDriver(
        UserManager<IUser> userManager,
        IOptions<IdentityOptions> identityOptions,
        IStringLocalizer<RegisterUserFormDisplayDriver> stringLocalizer
        )
    {
        _userManager = userManager;
        _identityOptions = identityOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RegisterUserForm model, BuildEditorContext context)
    {
        return Initialize<RegisterViewModel>("RegisterUserFormIdentifier", vm =>
        {
            vm.UserName = model.UserName;
            vm.Email = model.Email;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(RegisterUserForm model, UpdateEditorContext context)
    {
        var vm = new RegisterViewModel();

        await context.Updater.TryUpdateModelAsync(vm, Prefix);

        if (!string.IsNullOrEmpty(vm.UserName) && await _userManager.FindByNameAsync(vm.UserName) != null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(vm.UserName), S["A user with the same username already exists."]);
        }
        else if (_identityOptions.User.RequireUniqueEmail && !string.IsNullOrEmpty(vm.Email) && await _userManager.FindByEmailAsync(vm.Email) != null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(vm.Email), S["A user with the same email address already exists."]);
        }

        model.UserName = vm.UserName;
        model.Email = vm.Email;
        model.Password = vm.Password;

        return Edit(model, context);
    }
}
