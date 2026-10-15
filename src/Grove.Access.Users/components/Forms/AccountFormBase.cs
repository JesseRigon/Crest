using System.Globalization;
using Grove.Display.Components.Primitives;
using Microsoft.AspNetCore.Components;

namespace Grove.Access.Users.Components;

/// <summary>
/// What the registration and password recovery forms share: the form's extensions (a captcha
/// section) loaded once interactive, the headers its sections contribute, and a submission that
/// goes where the API's answer says (<see cref="AccountResult.Redirect"/>) or shows its errors.
/// </summary>
public abstract class AccountFormBase : ComponentBase
{
    [Inject]
    protected HttpClient Http { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected IServiceProvider Services { get; set; } = default!;

    /// <summary>The sign-in form the extensions are asked for (<see cref="AccessForms"/>).</summary>
    protected abstract string FormName { get; }

    protected AccessForm Form { get; private set; } = AccessForm.Empty;

    protected Dictionary<string, string?> Headers { get; } = [];

    protected AccountFieldError[] Errors { get; set; } = [];

    protected bool Busy { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        if (RendererInfo.IsInteractive)
        {
            Form = await SignInApi.GetAsync<AccessForm>(Http, "api/grove/auth/form/" + FormName) ?? AccessForm.Empty;
        }
    }

    /// <summary>Posts the form; a success goes to the answer's page (a new document: the session may have changed).</summary>
    protected async Task SubmitAsync(string uri, object body)
    {
        Busy = true;
        Errors = [];
        try
        {
            var (result, refusal) = await SignInApi.PostAsync<AccountResult, AccountResult>(Http, uri, body, Headers);
            if (result is { Succeeded: true })
            {
                if (result.Redirect is { Length: > 0 } redirect)
                {
                    Navigation.NavigateTo(redirect, forceLoad: true);
                }

                return;
            }

            Errors = refusal?.Errors is { Length: > 0 } errors ? errors : [new AccountFieldError(string.Empty, L("The request could not be completed."))];
        }
        finally
        {
            Busy = false;
        }
    }

    protected string L(string text) => (Services.GetService(typeof(ILocalizer)) as ILocalizer)?.Get(text, CultureInfo.CurrentUICulture) ?? text;
}
