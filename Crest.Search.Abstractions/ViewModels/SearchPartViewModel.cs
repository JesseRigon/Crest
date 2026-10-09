using System.ComponentModel.DataAnnotations;

namespace Crest.Search.ViewModels;

public class SearchPartViewModel
{
    [Required]
    public string Placeholder { get; set; }

    public string IndexName { get; set; }
}
