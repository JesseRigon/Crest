#nullable enable

using System.ComponentModel.DataAnnotations;

namespace Grove.Setup.Models;

/// <summary>What the installation page posts.</summary>
public sealed class SetupForm
{
    [Required(ErrorMessage = "The site name is required.")]
    [StringLength(70)]
    public string? SiteName { get; set; }

    public string? RecipeName { get; set; }

    public string? SiteTimeZone { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }

    [Required(ErrorMessage = "The user name is required.")]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "The email is required.")]
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? PasswordConfirmation { get; set; }

    /// <summary>The setup token a tenant created with a secret needs (the <c>token</c> query value).</summary>
    public string? Secret { get; set; }
}
