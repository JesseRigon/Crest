using Crest.DisplayManagement.Views;
using Crest.Users.Models;

namespace Crest.Users.ViewModels;

public class LostPasswordViewModel : ShapeViewModel
{
    public LostPasswordViewModel()
        : base("TemplateUserLostPassword")
    {
    }

    public User User { get; set; }
    public string LostPasswordUrl { get; set; }
}
