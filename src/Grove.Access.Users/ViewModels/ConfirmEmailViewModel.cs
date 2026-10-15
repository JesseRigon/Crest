using Grove.Display.Management.Views;

namespace Grove.Access.Users.ViewModels;

public class ConfirmEmailViewModel : ShapeViewModel
{
    public ConfirmEmailViewModel()
         : base("TemplateUserConfirmEmail")
    {
    }

    public IUser User { get; set; }
    public string ConfirmEmailUrl { get; set; }
}
