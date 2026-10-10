using Crest.Lists.Models;

namespace Crest.Lists.ViewModels;

public class ListPartFilterViewModel
{
    public string DisplayText { get; set; }
    public ContentsStatus Status { get; set; } = ContentsStatus.Latest;
}
