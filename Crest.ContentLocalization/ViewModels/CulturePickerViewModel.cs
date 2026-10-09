using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.ContentLocalization.ViewModels;

public class CulturePickerViewModel
{
    [BindNever]
    public CultureInfo CurrentCulture { get; set; }

    [BindNever]
    public IEnumerable<CultureInfo> SupportedCultures { get; set; }
}
