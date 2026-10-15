using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Users.Models;
using Grove.Users.ViewModels;

namespace Grove.Users.Drivers;

public sealed class ForgotPasswordFormDisplayDriver : DisplayDriver<ForgotPasswordForm>
{
    public override IDisplayResult Edit(ForgotPasswordForm model, BuildEditorContext context)
    {
        return Initialize<ForgotPasswordViewModel>("ForgotPasswordFormIdentifier", vm =>
        {
            vm.UsernameOrEmail = model.UsernameOrEmail;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ForgotPasswordForm model, UpdateEditorContext context)
    {
        var viewModel = new ForgotPasswordViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.UsernameOrEmail = viewModel.UsernameOrEmail;

        return Edit(model, context);
    }
}

