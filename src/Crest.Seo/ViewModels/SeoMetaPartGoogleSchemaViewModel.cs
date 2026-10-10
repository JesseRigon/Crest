using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Seo.Models;

namespace Crest.Seo.ViewModels;

public class SeoMetaPartGoogleSchemaViewModel
{
    public string GoogleSchema { get; set; }

    [BindNever]
    public SeoMetaPart SeoMetaPart { get; set; }
}
