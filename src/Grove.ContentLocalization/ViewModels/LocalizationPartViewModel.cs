using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.ContentLocalization.Models;
using Grove.DisplayManagement.Views;

namespace Grove.ContentLocalization.ViewModels;

public class LocalizationPartViewModel : ShapeViewModel
{
    public string LocalizationSet { get; set; }
    public string Culture { get; set; }

    [BindNever]
    public CultureInfo CultureInfo => CultureInfo.GetCultureInfo(Culture);

    [BindNever]
    public LocalizationPart LocalizationPart { get; set; }

    [BindNever]
    public IEnumerable<LocalizationLinksViewModel> ContentItemCultures { get; set; }
}

public class LocalizationLinksViewModel
{
    public bool IsDeleted { get; set; }
    public string ContentItemId { get; set; }
    public CultureInfo Culture { get; set; }
}
