using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.Facebook.Settings;

public class FacebookSettings
{
    [Display(Name = "Application Identifier", Description = "The Application ID found on the Meta developer portal parameters.", GroupName = "Meta Integration", Order = 0)]
    [Required]
    public string AppId { get; set; }

    [Display(Name = "Secret Key", Description = "The Application secret key found on the Meta developer portal parameters.", GroupName = "Meta Integration", Order = 1)]
    [Required]
    [Setting(Secret = true, ProtectedBy = FacebookConstants.Features.Core)]
    public string AppSecret { get; set; }

    [Display(Name = "Init on every page", GroupName = "Meta Integration", Order = 4)]
    public bool FBInit { get; set; }

    [Display(Name = "Parameters for the FB.init() call", Description = "AppId and Version are filled automatically. See the FB.init reference on the Meta developer portal (https://developers.facebook.com/docs/javascript/reference/FB.init/v3.2) for more info.", GroupName = "Meta Integration", Order = 5)]
    [DataType(DataType.MultilineText)]
    public string FBInitParams { get; set; } = """
        status: true,
        xfbml: true,
        autoLogAppEvents: true
        """;

    [Display(Name = "Javascript Sdk js", Description = "Determines which sdk js file to load. Default is sdk.js but you can change it as needed, e.g. sdk/xfbml.customerchat.js to render the chat plugin.", GroupName = "Meta Integration", Order = 3)]
    [Required]
    public string SdkJs { get; set; } = "sdk.js";

    [Display(Name = "Javascript SDK Version", Description = "Determines which versions of the Graph API and any API dialogs or plugins are invoked. Default is v3.2", GroupName = "Meta Integration", Order = 2)]
    [RegularExpression(@"(v)\d+\.\d+")]
    public string Version { get; set; } = "v3.2";
}
