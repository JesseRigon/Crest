using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Taxonomies.Models;

namespace Crest.Taxonomies.ViewModels;

public class TaxonomyPartEditViewModel
{
    public string Hierarchy { get; set; }

    public string TermContentType { get; set; }

    [BindNever]
    public TaxonomyPart TaxonomyPart { get; set; }
}
