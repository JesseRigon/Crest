using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Seo.Models;

namespace Grove.Seo.ViewModels;

public class SeoMetaPartGoogleSchemaViewModel
{
    public string GoogleSchema { get; set; }

    [BindNever]
    public SeoMetaPart SeoMetaPart { get; set; }
}
