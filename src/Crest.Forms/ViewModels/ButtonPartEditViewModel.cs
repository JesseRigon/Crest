using System.ComponentModel.DataAnnotations;

namespace Crest.Forms.ViewModels;

public class ButtonPartEditViewModel
{
    [Required]
    public string Text { get; set; }

    public string Type { get; set; }
}
