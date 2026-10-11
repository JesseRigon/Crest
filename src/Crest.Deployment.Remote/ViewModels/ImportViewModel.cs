using Microsoft.AspNetCore.Http;

namespace Crest.Deployment.Remote.ViewModels;

public class ImportViewModel
{
    /// <summary>Informational: the credential names the client (the Authorization header).</summary>
    public string ClientName { get; set; }

    public IFormFile Content { get; set; }
}
