using System.ComponentModel.DataAnnotations;

namespace Grove.Microsoft.Authentication.Settings;

public class AzureADSettings
{
    [Display(Name = "Display Name", Description = "The display name of the provider.", Order = 0)]
    [Required]
    public string DisplayName { get; set; }

    [Display(Name = "AppId", Description = "The Application ID found on the Microsoft Entra ID App registration parameters.", Order = 1)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Application Id is required")]
    public string AppId { get; set; }

    [Display(Name = "TenantId", Description = "The Tenant (Directory) ID found on the Microsoft Entra ID App registration parameters. For multi-tenant App registrations, use 'common' or 'organizations'.", Order = 2)]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Tenant Id is required")]
    public string TenantId { get; set; }

    /// <summary>
    /// The callback path; the handler's default when empty.
    /// </summary>
    [Display(Name = "Callback path", Description = "The request path within the application's base path where the user-agent will be returned. The middleware will process this request when it arrives.", Prompt = "/signin-oidc", Order = 3)]
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    [Display(Name = "Store tokens", Description = "Store external authentication tokens for reuse", Order = 4)]
    public bool SaveTokens { get; set; }
}
