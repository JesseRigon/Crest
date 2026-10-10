using Crest.Workflows.Api.Client.Resources.Identity.Contracts;
using Crest.Workflows.Api.Client.Resources.Identity.Requests;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Login.Contracts;
using Crest.Workflows.Studio.Login.Models;

namespace Crest.Workflows.Studio.Login.Services;

/// <summary>
/// A implementation of <see cref="ICredentialsValidator"/> that consumes the endpoints from Crest.Workflows.Identity.
/// </summary>
public class CrestWorkflowsIdentityCredentialsValidator : ICredentialsValidator
{
    private readonly IBackendApiClientProvider _backendApiClientProvider;
    /// <summary>
    /// Initializes a new instance of the <see cref="CrestWorkflowsIdentityCredentialsValidator"/> class.
    /// </summary>
    public CrestWorkflowsIdentityCredentialsValidator(IBackendApiClientProvider backendApiClientProvider)
    {
        _backendApiClientProvider = backendApiClientProvider;
    }

    /// <inheritdoc />
    public async ValueTask<ValidateCredentialsResult> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var api = await _backendApiClientProvider.GetApiAsync<ILoginApi>(cancellationToken);

        var request = new LoginRequest(username, password);
        var response = await api.LoginAsync(request, cancellationToken);

        return new ValidateCredentialsResult(response.IsAuthenticated, response.AccessToken, response.RefreshToken);
    }
}