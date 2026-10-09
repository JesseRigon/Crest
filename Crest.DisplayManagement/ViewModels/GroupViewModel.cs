using Crest.DisplayManagement.Shapes;

namespace Crest.DisplayManagement.ViewModels;

public class GroupViewModel : Shape
{
    public string Identifier { get; set; }
}

public class GroupingsViewModel : GroupViewModel
{
    public IGrouping<string, object>[] Groupings { get; set; }
}

public class GroupingViewModel : GroupViewModel
{
    public IGrouping<string, object> Grouping { get; set; }
}
