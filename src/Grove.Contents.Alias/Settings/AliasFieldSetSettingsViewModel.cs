using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Contents.Alias.Settings;

public class AliasFieldSetSettingsViewModel
{
    public string Pattern { get; set; }
    public AliasFieldSetOptions Options { get; set; }
    [BindNever]
    public AliasFieldSetSettings AliasFieldSetSettings { get; set; }
}
