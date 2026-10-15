using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;
using Grove.Users.Models;
using Grove.Users.ViewModels;

namespace Grove.Users.Drivers;

public sealed class LoginFormDisplayDriver : DisplayDriver<LoginForm>
{
    private readonly ISiteService _siteService;

    public LoginFormDisplayDriver(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public override IDisplayResult Edit(LoginForm model, BuildEditorContext context)
    {
        return Initialize<LoginViewModel>("LoginFormCredentials", vm =>
        {
            var loginSettings = _siteService.GetSettings<LoginSettings>();

            vm.UserName = model.UserName;
            vm.RememberMe = model.RememberMe;
            vm.AllowRememberMe = loginSettings.AllowRememberMe;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(LoginForm model, UpdateEditorContext context)
    {
        var viewModel = new LoginViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix,
            sp => sp.UserName,
            sp => sp.Password,
            sp => sp.RememberMe);

        var loginSettings = _siteService.GetSettings<LoginSettings>();

        model.UserName = viewModel.UserName;
        model.Password = viewModel.Password;
        model.RememberMe = loginSettings.AllowRememberMe && viewModel.RememberMe;

        return Edit(model, context);
    }
}
