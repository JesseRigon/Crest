using Grove.Display.Management.Views;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.ViewModels;

public class LostPasswordViewModel : ShapeViewModel
{
    public LostPasswordViewModel()
        : base("TemplateUserLostPassword")
    {
    }

    public User User { get; set; }
    public string LostPasswordUrl { get; set; }
}
