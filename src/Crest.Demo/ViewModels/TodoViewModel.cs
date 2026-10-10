using System.ComponentModel.DataAnnotations;
using Crest.DisplayManagement.Views;

namespace Crest.Demo.ViewModels;

public class TodoViewModel : ShapeViewModel
{
    public TodoViewModel()
        : base("Todo")
    {
    }

    public string TodoId { get; set; }

    [Required]
    public string Text { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    public bool IsCompleted { get; set; }

    public string DisplayMode
    {
        get
        {
            return Metadata.DisplayType;
        }
        set
        {
            var alternate = $"Todo_{value}";
            if (Metadata.Alternates.Contains(alternate))
            {
                if (Metadata.Alternates.Last == alternate)
                {
                    return;
                }

                Metadata.Alternates.Remove(alternate);
            }

            Metadata.Alternates.Add(alternate);
            Metadata.DisplayType = value;
        }
    }
}
