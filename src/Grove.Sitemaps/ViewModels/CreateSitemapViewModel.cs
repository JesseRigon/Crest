using System.ComponentModel.DataAnnotations;

namespace Grove.Sitemaps.ViewModels;

public class CreateSitemapViewModel
{
    [Required]
    public string Name { get; set; }

    public string Path { get; set; }

    public bool Enabled { get; set; } = true;
}
