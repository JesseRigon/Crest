using Microsoft.AspNetCore.Html;

namespace Grove.DisplayManagement;

public interface IDisplayHelper
{
    Task<IHtmlContent> ShapeExecuteAsync(IShape shape);
}
