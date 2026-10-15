using Grove.Lists.Models;

namespace Grove.Lists.ViewModels;

public class ListPartFilterViewModel
{
    public string DisplayText { get; set; }
    public ContentsStatus Status { get; set; } = ContentsStatus.Latest;
}
