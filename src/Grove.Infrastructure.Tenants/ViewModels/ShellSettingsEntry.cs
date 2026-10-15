using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Environment.Shell;

namespace Grove.Infrastructure.Tenants.ViewModels;

public class ShellSettingsEntry
{
    public string Category { get; set; }

    public string Description { get; set; }

    public bool Selected { get; set; }

    public string Name { get; set; }

    public string Token { get; set; }

    [BindNever]
    public ShellSettings ShellSettings { get; set; }
}
