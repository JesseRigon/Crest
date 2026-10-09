using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Users.Models;
using Crest.Users.ViewModels;

namespace Crest.Users.Drivers;

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

