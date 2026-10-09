using System.ComponentModel.DataAnnotations;

namespace Crest.Twitter.Signin.ViewModels;

public class TwitterSigninSettingsViewModel
{
    [RegularExpression(@"\/[-A-Za-z0-9+&@#\/%?=~_|!:,.;]+[-A-Za-z0-9+&@#\/%=~_|]", ErrorMessage = "Invalid path")]
    public string CallbackPath { get; set; }

    public bool SaveTokens { get; set; }
}
