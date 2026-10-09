namespace Crest.Cors.ViewModels;

public class CorsSettingsViewModel
{
    public CorsPolicyViewModel[] Policies { get; set; }

    public string DefaultPolicyName { get; set; }
}
