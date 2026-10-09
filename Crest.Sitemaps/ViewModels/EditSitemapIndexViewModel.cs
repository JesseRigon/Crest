using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.ViewModels;

public class EditSitemapIndexViewModel : CreateSitemapIndexViewModel
{
    [Required]
    public string SitemapId { get; set; }

    [BindNever]
    public SitemapIndexSource SitemapIndexSource { get; set; }
}
