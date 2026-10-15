using Grove.DisplayManagement.Views;
using Grove.Users.Models;

namespace Grove.Users.ViewModels;

public class LostPasswordViewModel : ShapeViewModel
{
    public LostPasswordViewModel()
        : base("TemplateUserLostPassword")
    {
    }

    public User User { get; set; }
    public string LostPasswordUrl { get; set; }
}
